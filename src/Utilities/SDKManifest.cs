// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// What should happen if multiple versions of a given productfamily or sdk name are found
    /// </summary>
    public enum MultipleVersionSupport
    {
        /// <summary>
        /// No action should be taken if multiple versions are detected
        /// </summary>
        Allow = 0,

        /// <summary>
        /// Log  warning
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Log an error
        /// </summary>
        Error = 2
    }

    /// <summary>
    /// Structure to represent the information contained in SDKManifest.xml
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Not worth breaking partners")]
    public class SDKManifest
    {
        /// <summary>
        /// Pattern in path to extension SDK used to help determine if manifest is from a framework SDK
        /// </summary>
        private static string s_extensionSDKPathPattern = @"\MICROSOFT SDKS\WINDOWS\V8.0\EXTENSIONSDKS";

        /// <summary>
        /// Default version of MaxPlatformVersion in framework extension SDKs with manifest not containing such a property
        /// </summary>
        private static string s_defaultMaxPlatformVersion = "8.0";

        /// <summary>
        /// Default version of MinOSVersion in framework extension SDKs with manifest not containing such a property
        /// </summary>
        private static string s_defaultMinOSVersion = "6.2.1";

        /// <summary>
        /// Default version of MaxOSVersionTested in framework extension SDKs with manifest not containing such a property
        /// </summary>
        private static string s_defaultMaxOSVersionTested = "6.2.1";

        /// <summary>
        /// What should happen if this sdk is resolved with other sdks of the same productfamily or same sdk name.
        /// </summary>
        private MultipleVersionSupport _supportsMultipleVersions;

        /// <summary>
        /// Path to where the file SDKManifest.xml is stored
        /// </summary>
        private readonly string _pathToSdk;

        /// <summary>
        /// Whatever appx locations we found in the manifest
        /// </summary>
        private IDictionary<string, string> _appxLocations;

        /// <summary>
        /// Whatever framework identities we found in the manifest.
        /// </summary>
        private IDictionary<string, string> _frameworkIdentities;

        /// <summary>
        /// Whatever MaxOSVersionTested we found in the manifest.
        /// </summary>
        private string _maxOSVersionTested;

        /// <summary>
        /// Whatever MinOSVersion we found in the manifest
        /// </summary>
        private string _minOSVersion;

        /// <summary>
        /// Whatever MaxPlatformVersion we found in the manifest
        /// </summary>
        private string _maxPlatformVersion;

        /// <summary>
        /// The SDKType, default of unspecified
        /// </summary>
        private SDKType _sdkType = SDKType.Unspecified;

        /// <summary>
        /// Constructor
        /// Takes the path to SDKManifest.xml and populates the structure with manifest data
        /// </summary>
        public SDKManifest(string pathToSdk)
        {
            ErrorUtilities.VerifyThrowArgumentLength(pathToSdk, nameof(pathToSdk));
            _pathToSdk = pathToSdk;
            LoadManifestFile();
        }

        /// <summary>
        /// Whatever information regarding support for multiple versions is found in the manifest
        /// </summary>
        public MultipleVersionSupport SupportsMultipleVersions => _supportsMultipleVersions;

        /// <summary>
        /// Whatever framework identities we found in the manifest.
        /// </summary>
        public IDictionary<string, string> FrameworkIdentities => _frameworkIdentities != null ? new ReadOnlyDictionary<string, string>(_frameworkIdentities) : null;

        /// <summary>
        /// Whatever appx locations we found in the manifest
        /// </summary>
        public IDictionary<string, string> AppxLocations => _appxLocations != null ? new ReadOnlyDictionary<string, string>(_appxLocations) : null;

        /// <summary>
        /// PlatformIdentity if it exists in the appx manifest for this sdk.
        /// </summary>
        public string PlatformIdentity { get; private set; }

        /// <summary>
        /// The FrameworkIdentity for the sdk, this may be a single name or a | delimited name
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Necessary for compatibility with specs of SDKManifest.xml")]
        public string FrameworkIdentity { get; private set; }

        /// <summary>
        /// Support Prefer32bit found in the sdk manifest
        /// </summary>
        public string SupportPrefer32Bit { get; private set; }

        /// <summary>
        /// SDKType found in the sdk manifest
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Want to keep same case as the attribute in SDKManifest.xml")]
        public SDKType SDKType => _sdkType;

        /// <summary>
        /// CopyRedistToSubDirectory specifies where the redist files should be copied to relative to the root of the package.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "SubDirectory", Justification = "Want to keep case compliant with the attributes in the SDKManifest.xml")]
        public string CopyRedistToSubDirectory { get; private set; }

        /// <summary>
        /// Supported Architectures is a semicolon delimited list of architectures that the SDK supports.
        /// </summary>
        public string SupportedArchitectures { get; private set; }

        /// <summary>
        /// DependsOnSDK is a semicolon delimited list of SDK identities that the SDK requires be resolved in order to function.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Not worth breaking consumers")]
        public string DependsOnSDK { get; private set; }

        /// <summary>
        /// ProductFamilyName specifies the product family for the SDK. This is offered up as metadata on the resolved sdkreference and is used to detect sdk conflicts.
        /// </summary>
        public string ProductFamilyName { get; private set; }

        /// <summary>
        /// The platform the SDK targets.
        /// </summary>
        public string TargetPlatform { get; private set; }

        /// <summary>
        /// Minimum version of the platform the SDK supports.
        /// </summary>
        public string TargetPlatformMinVersion { get; private set; }

        /// <summary>
        /// Maximum version of the platform that the SDK supports.
        /// </summary>
        public string TargetPlatformVersion { get; private set; }

        /// <summary>
        /// DisplayName found in the sdk manifest
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// MinVSVersion found in the sdk manifest
        /// </summary>
        public string MinVSVersion { get; private set; }

        /// <summary>
        /// MinOSVersion found in the sdk manifest, defaults to 6.2.1 for framework extension SDKs when manifest does not have this property set
        /// </summary>
        public string MinOSVersion => _minOSVersion == null && IsFrameworkExtensionSdkManifest
            ? s_defaultMinOSVersion
            : _minOSVersion;

        /// <summary>
        /// MaxPlatformVersion found in the sdk manifest, defaults to 8.0 for framework extension SDKs when manifest does not have this property set
        /// </summary>
        public string MaxPlatformVersion => _maxPlatformVersion == null && IsFrameworkExtensionSdkManifest
            ? s_defaultMaxPlatformVersion
            : _maxPlatformVersion;

        /// <summary>
        /// MaxOSVersionTested found in the sdk manifest, defaults to 6.2.1 for framework extension SDKs when manifest does not have this property set
        /// </summary>
        public string MaxOSVersionTested => _maxOSVersionTested == null && IsFrameworkExtensionSdkManifest
            ? s_defaultMaxOSVersionTested
            : _maxOSVersionTested;

        /// <summary>
        /// MoreInfo as found in the sdk manifest
        /// </summary>
        public string MoreInfo { get; private set; }

        /// <summary>
        /// Flag set to true if an exception occurred while reading the manifest
        /// </summary>
        public bool ReadError { get; private set; }

        /// <summary>
        /// Message from exception thrown while reading manifest
        /// </summary>
        public string ReadErrorMessage { get; private set; }

        /// <summary>
        /// The contracts contained by this manifest, if any
        /// Item1: Contract name
        /// Item2: Contract version
        /// </summary>
        internal ICollection<ApiContract> ApiContracts { get; private set; }

        /// <summary>
        /// Decide on whether it is a framework extension sdk based on manifest's FrameworkIdentify and path
        /// </summary>
        private bool IsFrameworkExtensionSdkManifest => _frameworkIdentities?.Count > 0
                                                        && _pathToSdk?.ToUpperInvariant().Contains(s_extensionSDKPathPattern) == true;

        /// <summary>
        /// Load content of SDKManifest.xml
        /// </summary>
        private void LoadManifestFile()
        {
            /*
               Extension SDK Manifest:
               <FileList
                    TargetPlatform="UAP"
                    TargetPlatformMinVersion="1.0.0.0"
                    TargetPlatformVersion="1.0.0.0"
                    SDKType = "Platform" | "Framework" | "External" 
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    FrameworkIdentity-Debug = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    FrameworkIdentity-Retail = ""Name=MySDK.10, MinVersion=1.0.0.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    AppliesTo = ""WindowsAppContainer + WindowsXAML""
                    SupportPrefer32Bit = ""True""
                    SupportedArchitectures = ""x86;x64;ARM""
                    SupportsMultipleVersions = ""Error""
                    AppX-Debug-x86 = "".\AppX\Debug\x86\Microsoft.MySDK.x86.Debug.1.0.appx""
                    AppX-Debug-x64 = "".\AppX\Debug\x64\Microsoft.MySDK.x64.Debug.1.0.appx""
                    AppX-Debug-ARM = "".\AppX\Debug\ARM\Microsoft.MySDK.ARM.Debug.1.0.appx""
                    AppX-Retail-x86 = "".\AppX\Retail\x86\Microsoft.MySDK.x86.1.0.appx""
                    AppX-Retail-x64 = "".\AppX\Retail\x64\Microsoft.MySDK.x64.1.0.appx""
                    AppX-Retail-ARM = "".\AppX\Retail\ARM\Microsoft.MySDK.ARM.1.0.appx"" 
                    CopyRedistToSubDirectory = "".""
                    DependsOn = ""SDKB, version=2.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxPlatformVersion = ""8.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1"">

                    <!-- New Style -->
                    <ContainedApiContracts>
                        <ApiContract name="UAP" version="1.0.0.0" />
                    </ContainedApiContracts>

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>
               
               Platform SDK Manifest:
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
                    <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>
             */
            string sdkManifestPath = Path.Combine(_pathToSdk, "SDKManifest.xml");

            try
            {
                if (FileSystems.Default.FileExists(sdkManifestPath))
                {
                    XmlDocument doc = new XmlDocument();
                    XmlReaderSettings readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };

                    using (XmlReader xmlReader = XmlReader.Create(sdkManifestPath, readerSettings))
                    {
                        doc.Load(xmlReader);
                    }

                    XmlElement rootElement = null;

                    foreach (XmlNode childNode in doc.ChildNodes)
                    {
                        if (childNode.NodeType == XmlNodeType.Element &&
                            string.Equals(childNode.Name, Elements.FileList, StringComparison.Ordinal))
                        {
                            rootElement = (XmlElement)childNode;
                            break;
                        }
                    }

                    if (rootElement != null)
                    {
                        ReadFileListAttributes(rootElement.Attributes);
                        foreach (XmlNode childNode in rootElement.ChildNodes)
                        {
                            XmlElement childElement = childNode as XmlElement;
                            if (childElement == null)
                            {
                                continue;
                            }

                            if (ApiContract.IsContainedApiContractsElement(childElement.Name))
                            {
                                ApiContracts = new List<ApiContract>();
                                ApiContract.ReadContractsElement(childElement, ApiContracts);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                ReadError = true;
                ReadErrorMessage = e.Message;
            }
        }

        /// <summary>
        /// Reads the attributes from the "FileList" element of the SDK manifest.
        /// </summary>
        private void ReadFileListAttributes(XmlAttributeCollection attributes)
        {
            foreach (XmlAttribute attribute in attributes.OfType<XmlAttribute>())
            {
                string value = attribute.Value.Trim();
                if (value.Length > 0)
                {
                    if (attribute.Name.StartsWith(Attributes.FrameworkIdentity, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_frameworkIdentities == null)
                        {
                            _frameworkIdentities = new Dictionary<string, string>();
                        }

                        _frameworkIdentities.Add(attribute.Name, value);
                        continue;
                    }

                    if (attribute.Name.StartsWith(Attributes.APPX, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_appxLocations == null)
                        {
                            _appxLocations = new Dictionary<string, string>();
                        }

                        _appxLocations.Add(attribute.Name, value);
                        continue;
                    }

                    switch (attribute.Name)
                    {
                        case Attributes.TargetPlatform:
                            TargetPlatform = value;
                            break;
                        case Attributes.TargetPlatformMinVersion:
                            TargetPlatformMinVersion = value;
                            break;
                        case Attributes.TargetPlatformVersion:
                            TargetPlatformVersion = value;
                            break;
                        case Attributes.MinVSVersion:
                            MinVSVersion = value;
                            break;
                        case Attributes.MinOSVersion:
                            _minOSVersion = value;
                            break;
                        case Attributes.MaxOSVersionTested:
                            _maxOSVersionTested = value;
                            break;
                        case Attributes.MaxPlatformVersion:
                            _maxPlatformVersion = value;
                            break;
                        case Attributes.PlatformIdentity:
                            PlatformIdentity = value;
                            break;
                        case Attributes.SupportPrefer32Bit:
                            SupportPrefer32Bit = value;
                            break;
                        case Attributes.SupportsMultipleVersions:
                            _supportsMultipleVersions = ParseSupportMultipleVersions(value);
                            break;
                        case Attributes.SDKType:
                            Enum.TryParse(value, out _sdkType);
                            break;
                        case Attributes.DisplayName:
                            DisplayName = value;
                            break;
                        case Attributes.MoreInfo:
                            MoreInfo = value;
                            break;
                        case Attributes.CopyRedistToSubDirectory:
                            CopyRedistToSubDirectory = value;
                            break;
                        case Attributes.SupportedArchitectures:
                            SupportedArchitectures = value;
                            break;
                        case Attributes.DependsOnSDK:
                            DependsOnSDK = value;
                            break;
                        case Attributes.ProductFamilyName:
                            ProductFamilyName = value;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Parse the multipleversions string. Returns MultipleVersionSupport.Allow if it cannot be parsed correctly.
        /// </summary>
        private static MultipleVersionSupport ParseSupportMultipleVersions(string multipleVersionsValue)
            => !string.IsNullOrEmpty(multipleVersionsValue) && Enum.TryParse(multipleVersionsValue, /*ignoreCase*/true, out MultipleVersionSupport supportsMultipleVersions)
            ? supportsMultipleVersions
            : MultipleVersionSupport.Allow;

        /// <summary>
        /// Helper class with attributes of SDKManifest.xml
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "Microsoft.Build.Utilities.SDKManifest+Attributes", Justification = "Not worth breaking customers / spending resources to fix.")]
        public static class Attributes
        {
            /// <summary>
            /// Platform that the SDK targets
            /// </summary>
            public const string TargetPlatform = "TargetPlatform";

            /// <summary>
            /// The minimum version of the platform that the SDK targets
            /// </summary>
            public const string TargetPlatformMinVersion = "TargetPlatformMinVersion";

            /// <summary>
            /// The max version of the platform that the SDK targets
            /// </summary>
            public const string TargetPlatformVersion = "TargetPlatformVersion";

            /// <summary>
            /// Framework Identity metadata name and manifest attribute
            /// </summary>
            public const string FrameworkIdentity = "FrameworkIdentity";

            /// <summary>
            /// Supported Architectures metadata name and manifest attribute
            /// </summary>
            public const string SupportedArchitectures = "SupportedArchitectures";

            /// <summary>
            /// Prefer32BitSupport metadata name and manifest attribute
            /// </summary>
            public const string SupportPrefer32Bit = "SupportPrefer32Bit";

            /// <summary>
            /// AppxLocation metadata
            /// </summary>
            public const string AppxLocation = "AppxLocation";

            /// <summary>
            /// APPX manifest attribute
            /// </summary>
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "APPX", Justification = "Want to keep same case as the attribute in SDKManifest.xml")]
            public const string APPX = "APPX";

            /// <summary>
            /// PlatformIdentity  metadata name and manifest attribute
            /// </summary>
            public const string PlatformIdentity = "PlatformIdentity";

            /// <summary>
            /// SDKType metadata name and manifest attribute
            /// </summary>
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Want to keep same case as the attribute in SDKManifest.xml")]
            public const string SDKType = "SDKType";

            /// <summary>
            /// DisplayName metadata name and manifest attribute
            /// </summary>
            public const string DisplayName = "DisplayName";

            /// <summary>
            /// CopyRedistToSubDirectory metadata name and manifest attribute
            /// </summary>
            [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "SubDirectory", Justification = "Want to keep same case as in SDKManifest.sdk")]
            public const string CopyRedistToSubDirectory = "CopyRedistToSubDirectory";

            /// <summary>
            /// ProductFamilyName metadata name and manifest attribute
            /// </summary>
            public const string ProductFamilyName = "ProductFamilyName";

            /// <summary>
            /// SupportsMultipleVersions metadata name and manifest attribute
            /// </summary>
            public const string SupportsMultipleVersions = "SupportsMultipleVersions";

            /// <summary>
            /// TargetedSDKArchitecture metadata name
            /// </summary>
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Not worth breaking customers")]
            public const string TargetedSDK = "TargetedSDKArchitecture";

            /// <summary>
            /// TargetedSDKConfiguration metadata name
            /// </summary>
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Not worth breaking customers")]
            public const string TargetedSDKConfiguration = "TargetedSDKConfiguration";

            /// <summary>
            /// ExpandReferenceAssemblies metadata name
            /// </summary>
            public const string ExpandReferenceAssemblies = "ExpandReferenceAssemblies";

            /// <summary>
            /// DependsOn metadata name
            /// </summary>
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Not worth breaking customers")]
            public const string DependsOnSDK = "DependsOn";

            /// <summary>
            /// CopyRedist metadata name
            /// </summary>
            public const string CopyRedist = "CopyRedist";

            /// <summary>
            /// CopyLocalExpandedReferenceAssemblies metadata name
            /// </summary>
            public const string CopyLocalExpandedReferenceAssemblies = "CopyLocalExpandedReferenceAssemblies";

            // Dev 12 new attributes

            /// <summary>
            /// MinOSVersion metadata name
            /// </summary>
            public const string MinOSVersion = "MinOSVersion";

            /// <summary>
            /// MinVSVersion metadata name
            /// </summary>
            public const string MinVSVersion = "MinVSVersion";

            /// <summary>
            /// MaxPlatformVersionAttribute metadata name
            /// </summary>
            public const string MaxPlatformVersion = "MaxPlatformVersion";

            /// <summary>
            /// MoreInfoAttribute metadata name
            /// </summary>
            public const string MoreInfo = "MoreInfo";

            /// <summary>
            /// MaxOSVersionTestedAttribute metadata name
            /// </summary>
            public const string MaxOSVersionTested = "MaxOSVersionTested";
        }

        /// <summary>
        /// Helper class with elements of SDKManifest.xml
        /// </summary>
        private static class Elements
        {
            /// <summary>
            /// Root element 
            /// </summary>
            public const string FileList = "FileList";
        }
    }
}
