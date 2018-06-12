// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes a ClickOnce or native Win32 application manifest.
    /// </summary>
    /// <remarks>
    /// This is a serialization format, don't remove the private fields.
    /// </remarks>
    [ComVisible(false)]
    [XmlRoot("ApplicationManifest")]
    public sealed class ApplicationManifest : AssemblyManifest
    {
        private string _configFile;
        private AssemblyIdentity _entryPointIdentity;
        private AssemblyReference _entryPoint;
        private string _entryPointParameters;
        private string _entryPointPath;
        private string _errorReportUrl;
        private string _iconFile;
        private bool _isClickOnceManifest = true;
        private string _oSMajor;
        private string _oSMinor;
        private string _oSBuild;
        private string _oSRevision;
        private string _oSSupportUrl;
        private string _oSDescription;
        private TrustInfo _trustInfo;
        private int _maxTargetPath;
        private bool _hostInBrowser;
        private bool _useApplicationTrust;
        private string _product;
        private string _publisher;
        private string _suiteName;
        private string _supportUrl;
        private FileAssociation[] _fileAssociations;
        private FileAssociationCollection _fileAssociationList;
        private string _targetFrameworkVersion;

        /// <summary>
        /// Initializes a new instance of the ApplicationManifest class.
        /// </summary>
        public ApplicationManifest()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ApplicationManifest class.
        /// </summary>
        public ApplicationManifest(string targetFrameworkVersion)
        {
            _targetFrameworkVersion = targetFrameworkVersion;
        }

        /// <summary>
        /// Indicates the application configuration file.
        /// For a Win32 native manifest, this input is ignored.
        /// </summary>
        [XmlIgnore]
        public string ConfigFile
        {
            get => _configFile;
            set => _configFile = value;
        }

        [XmlIgnore]
        public override AssemblyReference EntryPoint
        {
            get
            {
                FixupEntryPoint();
                return _entryPoint;
            }
            set
            {
                _entryPoint = value;
                UpdateEntryPoint();
            }
        }

        /// <summary>
        /// Specifies the target framework version
        /// </summary>
        [XmlIgnore]
        public string TargetFrameworkVersion
        {
            get => _targetFrameworkVersion;
            set => _targetFrameworkVersion = value;
        }

        /// <summary>
        /// Specifies the link to use if there is a failure launching the application.
        /// The specified value should be a fully qualified URL or UNC path.
        /// </summary>
        [XmlIgnore]
        public string ErrorReportUrl
        {
            get => _errorReportUrl;
            set => _errorReportUrl = value;
        }

        // Make sure we have a CLR dependency, add it if not...
        private void FixupClrVersion()
        {
            AssemblyReference CLRPlatformAssembly = AssemblyReferences.Find(Constants.CLRPlatformAssemblyName);
            if (CLRPlatformAssembly == null)
            {
                CLRPlatformAssembly = new AssemblyReference { IsPrerequisite = true };
                AssemblyReferences.Add(CLRPlatformAssembly);
            }
            if (String.IsNullOrEmpty(CLRPlatformAssembly.AssemblyIdentity?.Version))
            {
                CLRPlatformAssembly.AssemblyIdentity = new AssemblyIdentity(Constants.CLRPlatformAssemblyName, Util.GetClrVersion(_targetFrameworkVersion));
            }
        }

        private void FixupEntryPoint()
        {
            if (_entryPoint == null)
            {
                _entryPoint = AssemblyReferences.Find(_entryPointIdentity);
            }
        }

        // WinXP is required if app has any native assembly references or any RegFree COM definitions...
        private bool WinXPRequired
        {
            get
            {
                foreach (FileReference f in FileReferences)
                {
                    if (f.ComClasses != null || f.TypeLibs != null || f.ProxyStubs != null)
                    {
                        return true;
                    }
                }
                foreach (AssemblyReference a in AssemblyReferences)
                {
                    if (a.ReferenceType == AssemblyReferenceType.NativeAssembly)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        [XmlIgnore]
        public FileAssociationCollection FileAssociations => _fileAssociationList ??
                                                             (_fileAssociationList = new FileAssociationCollection(_fileAssociations));

        /// <summary>
        /// If true, the application will run in IE using WPF's xbap application model.
        /// </summary>
        [XmlIgnore]
        public bool HostInBrowser
        {
            get => _hostInBrowser;
            set => _hostInBrowser = value;
        }

        /// <summary>
        /// Indicates the application icon file.
        /// The application icon is expressed in the generated application manifest and is used for the start menu and Add/Remove Programs dialog.
        /// If this input is not specified then a default icon is used.
        /// For a Win32 native manifest, this input is ignored.
        /// </summary>
        [XmlIgnore]
        public string IconFile
        {
            get => _iconFile;
            set => _iconFile = value;
        }

        /// <summary>
        /// Indicates whether the manifest is a ClickOnce application manifest or a native Win32 application manifest.
        /// </summary>
        [XmlIgnore]
        public bool IsClickOnceManifest
        {
            get => _isClickOnceManifest;
            set => _isClickOnceManifest = value;
        }


        /// <summary>
        /// Specifies the maximum allowable length of a file path in a ClickOnce application deployment.
        /// If this value is specified, then the length of each file path in the application is checked against this limit.
        /// Any items that exceed the limit will result in a warning message.
        /// If this input is not specified or is zero, then no checking is performed.
        /// For a Win32 native manifest, this input is ignored.
        /// </summary>
        [XmlIgnore]
        public int MaxTargetPath
        {
            get => _maxTargetPath;
            set => _maxTargetPath = value;
        }

        internal override void OnBeforeSave()
        {
            FixupEntryPoint();
            if (_isClickOnceManifest)
            {
                FixupClrVersion();
            }
            base.OnBeforeSave();
            if (_isClickOnceManifest && AssemblyIdentity != null &&
                String.IsNullOrEmpty(AssemblyIdentity.PublicKeyToken))
            {
                AssemblyIdentity.PublicKeyToken = "0000000000000000";
            }
            UpdateEntryPoint();
            AssemblyIdentity.Type = "win32"; // Activation on WinXP gold will fail if type="win32" attribute is not present
            if (String.IsNullOrEmpty(OSVersion))
            {
                OSVersion = !WinXPRequired ? Constants.OSVersion_Win9X : Constants.OSVersion_WinXP;
            }

            if (_fileAssociationList != null)
            {
                _fileAssociations = _fileAssociationList.ToArray();
            }
        }

        /// <summary>
        /// Specifies a textual description for the OS dependency.
        /// </summary>
        [XmlIgnore]
        public string OSDescription
        {
            get => _oSDescription;
            set => _oSDescription = value;
        }

        /// <summary>
        /// Specifies a support URL for the OS dependency.
        /// </summary>
        [XmlIgnore]
        public string OSSupportUrl
        {
            get => _oSSupportUrl;
            set => _oSSupportUrl = value;
        }

        /// <summary>
        /// Specifies the minimum required OS version required by the application.
        /// An example value is "5.1.2600.0" for Windows XP.
        /// If this input is not specified a default value is used.
        /// The default value is the minimum supported OS of the .NET Framework, which is "4.10.0.0" for Windows 98SE.
        /// However, if the application contains any native or Reg-Free COM references, then the default will be the Windows XP version.
        /// For a Win32 native manifest, this input is ignored.
        /// </summary>
        [XmlIgnore]
        public string OSVersion
        {
            get
            {
                if (String.IsNullOrEmpty(_oSMajor)) return null;
                Version v;
                try
                {
                    v = new Version($"{_oSMajor}.{_oSMinor}.{_oSBuild}.{_oSRevision}");
                }
                catch (FormatException)
                {
                    return null;
                }
                return v.ToString();
            }
            set
            {
                if (value == null)
                {
                    _oSMajor = null;
                    _oSMinor = null;
                    _oSBuild = null;
                    _oSRevision = null;
                }
                else
                {
                    Version v = new Version(value);
                    if (v.Build < 0 || v.Revision < 0)
                    {
                        throw new FormatException();
                    }
                    _oSMajor = v.Major.ToString("G", CultureInfo.InvariantCulture);
                    _oSMinor = v.Minor.ToString("G", CultureInfo.InvariantCulture);
                    _oSBuild = v.Build.ToString("G", CultureInfo.InvariantCulture);
                    _oSRevision = v.Revision.ToString("G", CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>
        /// Specifies the name of the application.
        /// If this input is not specified then the Product is not written into the Application Manifest
        /// This name is used for the shortcut name on the Start menu and is part of the name that appears in the Add/Remove Programs dialog.
        /// </summary>
        [XmlIgnore]
        public string Product
        {
            get => _product;
            set => _product = value;
        }

        /// <summary>
        /// Specifies the publisher of the application.
        /// If this input is not specified then the Publisher is not written into the Application Manifest
        /// This name is used for the folder name on the Start menu and is part of the name that appears in the Add/Remove Programs dialog.
        /// </summary>
        [XmlIgnore]
        public string Publisher
        {
            get => _publisher;
            set => _publisher = value;
        }

        /// <summary>
        /// Specifies the suite name of the application.
        /// This name is used for the sub-folder name on the Start menu (as a child of the publisher)
        /// </summary>
        [XmlIgnore]
        public string SuiteName
        {
            get => _suiteName;
            set => _suiteName = value;
        }

        /// <summary>
        /// Specifies the link that appears in the Add/Remove Programs dialog for the application.
        /// The specified value should be a fully qualified URL or UNC path.
        /// </summary>
        [XmlIgnore]
        public string SupportUrl
        {
            get => _supportUrl;
            set => _supportUrl = value;
        }

        /// <summary>
        /// Specifies a trust object defining the application security.
        /// </summary>
        [XmlIgnore]
        public TrustInfo TrustInfo
        {
            get => _trustInfo;
            set => _trustInfo = value;
        }

        /// <summary>
        /// If true, the install will use the settings in the application manifest in the trust prompt.
        /// </summary>
        [XmlIgnore]
        public bool UseApplicationTrust
        {
            get => _useApplicationTrust;
            set => _useApplicationTrust = value;
        }

        private void UpdateEntryPoint()
        {
            if (_entryPoint != null)
            {
                _entryPointIdentity = new AssemblyIdentity(_entryPoint.AssemblyIdentity);
                _entryPointPath = _entryPoint.TargetPath;
            }
            else
            {
                _entryPointIdentity = null;
                _entryPointPath = null;
            }
        }

        public override void Validate()
        {
            base.Validate();
            if (_isClickOnceManifest)
            {
                ValidateReferencesForClickOnceApplication();
                ValidatePlatform();
                ValidateConfig();
                ValidateEntryPoint();
                ValidateFileAssociations();
            }
            else
            {
                ValidateReferencesForNativeApplication();
            }
            ValidateCom();
        }

        private void ValidateCom()
        {
            int t1 = Environment.TickCount;
            string outputFileName = Path.GetFileName(SourcePath);
            var clsidList = new Dictionary<string, ComInfo>();
            var tlbidList = new Dictionary<string, ComInfo>();

            // Check for duplicate COM definitions in all dependent manifests...
            foreach (AssemblyReference assembly in AssemblyReferences)
            {
                if (assembly.ReferenceType == AssemblyReferenceType.NativeAssembly && !assembly.IsPrerequisite && !String.IsNullOrEmpty(assembly.ResolvedPath))
                {
                    ComInfo[] comInfoArray = ManifestReader.GetComInfo(assembly.ResolvedPath); ;
                    if (comInfoArray != null)
                    {
                        foreach (ComInfo comInfo in comInfoArray)
                        {
                            if (!String.IsNullOrEmpty(comInfo.ClsId))
                            {
                                string key = comInfo.ClsId.ToLowerInvariant();
                                if (!clsidList.ContainsKey(key))
                                {
                                    clsidList.Add(key, comInfo);
                                }
                                else
                                {
                                    OutputMessages.AddErrorMessage("GenerateManifest.DuplicateComDefinition", "clsid", comInfo.ComponentFileName, comInfo.ClsId, comInfo.ManifestFileName, clsidList[key].ManifestFileName);
                                }
                            }
                            if (!String.IsNullOrEmpty(comInfo.TlbId))
                            {
                                string key = comInfo.TlbId.ToLowerInvariant();
                                if (!tlbidList.ContainsKey(key))
                                {
                                    tlbidList.Add(key, comInfo);
                                }
                                else
                                {
                                    OutputMessages.AddErrorMessage("GenerateManifest.DuplicateComDefinition", "tlbid", comInfo.ComponentFileName, comInfo.TlbId, comInfo.ManifestFileName, tlbidList[key].ManifestFileName);
                                }
                            }
                        }
                    }
                }
            }

            // Check for duplicate COM definitions in the manifest about to be generated...
            foreach (FileReference file in FileReferences)
            {
                if (file.ComClasses != null)
                {
                    foreach (ComClass comClass in file.ComClasses)
                    {
                        string key = comClass.ClsId.ToLowerInvariant();
                        if (!clsidList.ContainsKey(key))
                        {
                            clsidList.Add(key, new ComInfo(outputFileName, file.TargetPath, comClass.ClsId, null));
                        }
                        else
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.DuplicateComDefinition", "clsid", file.ToString(), comClass.ClsId, outputFileName, clsidList[key].ManifestFileName);
                        }
                    }
                }
                if (file.TypeLibs != null)
                {
                    foreach (TypeLib typeLib in file.TypeLibs)
                    {
                        string key = typeLib.TlbId.ToLowerInvariant();
                        if (!tlbidList.ContainsKey(key))
                        {
                            tlbidList.Add(key, new ComInfo(outputFileName, file.TargetPath, null, typeLib.TlbId));
                        }
                        else
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.DuplicateComDefinition", "tlbid", file.ToString(), typeLib.TlbId, outputFileName, tlbidList[key].ManifestFileName);
                        }
                    }
                }
            }

            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "GenerateManifest.CheckForComDuplicates t={0}", Environment.TickCount - t1));
        }

        private void ValidateConfig()
        {
            if (String.IsNullOrEmpty(ConfigFile)) return;
            FileReference configFile = FileReferences.FindTargetPath(ConfigFile);
            if (configFile == null) return;

            if (!TrustInfo.IsFullTrust)
            {
                var document = new XmlDocument();
                var xrs = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (XmlReader xr = XmlReader.Create(configFile.ResolvedPath, xrs))
                {
                    document.Load(xr);
                }
                XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);
                XmlNodeList nodes = document.SelectNodes(XPaths.configBindingRedirect, nsmgr);
                if (nodes.Count > 0)
                {
                    OutputMessages.AddWarningMessage("GenerateManifest.ConfigBindingRedirectsWithPartialTrust");
                }
            }
        }

        private void ValidateEntryPoint()
        {
            if (_entryPoint != null)
            {
                bool isCorrectFileType = !String.IsNullOrEmpty(_entryPoint.TargetPath) && _entryPoint.TargetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                if (!isCorrectFileType)
                {
                    OutputMessages.AddErrorMessage("GenerateManifest.InvalidEntryPoint", _entryPoint.ToString());
                }
            }
        }

        private void ValidateFileAssociations()
        {
            if (FileAssociations.Count > 0)
            {
                if (FileAssociations.Count > Constants.MaxFileAssociationsCount)
                {
                    OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationsCountExceedsMaximum", Constants.MaxFileAssociationsCount.ToString(CultureInfo.CurrentUICulture));
                }

                var usedExtensions = new Dictionary<string, FileAssociation>(StringComparer.OrdinalIgnoreCase);
                foreach (FileAssociation fileAssociation in FileAssociations)
                {
                    if (string.IsNullOrEmpty(fileAssociation.Extension) ||
                        string.IsNullOrEmpty(fileAssociation.Description) ||
                        string.IsNullOrEmpty(fileAssociation.ProgId) ||
                        string.IsNullOrEmpty(fileAssociation.DefaultIcon))
                    {
                        OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationMissingAttribute");
                    }
                    if (!string.IsNullOrEmpty(fileAssociation.Extension))
                    {
                        if (fileAssociation.Extension[0] != '.')
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationExtensionMissingLeadDot");
                        }
                        if (fileAssociation.Extension.Length > Constants.MaxFileAssociationExtensionLength)
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationExtensionTooLong", fileAssociation.Extension, Constants.MaxFileAssociationExtensionLength.ToString(CultureInfo.CurrentUICulture));
                        }
                        if (!usedExtensions.ContainsKey(fileAssociation.Extension))
                        {
                            usedExtensions.Add(fileAssociation.Extension, fileAssociation);
                        }
                        else
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationsDuplicateExtensions", fileAssociation.Extension);
                        }
                    }
                    if (!string.IsNullOrEmpty(fileAssociation.DefaultIcon))
                    {
                        FileReference defaultIconReference = null;
                        foreach (FileReference fileReference in FileReferences)
                        {
                            if (fileReference.TargetPath.Equals(fileAssociation.DefaultIcon, StringComparison.Ordinal))
                            {
                                defaultIconReference = fileReference;
                                break;
                            }
                        }
                        if (defaultIconReference == null || !string.IsNullOrEmpty(defaultIconReference.Group))
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationDefaultIconNotInstalled", fileAssociation.DefaultIcon);
                        }
                    }
                }

                if (!TrustInfo.IsFullTrust)
                {
                    OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationsApplicationNotFullTrust");
                }
                if (EntryPoint == null)
                {
                    OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationsNoEntryPoint");
                }
            }
        }

        private void ValidateReferencesForNativeApplication()
        {
            foreach (AssemblyReference assembly in AssemblyReferences)
            {
                // Check that the assembly identity matches the filename for all local dependencies...
                if (!assembly.IsPrerequisite && !String.Equals(
                        assembly.AssemblyIdentity.Name,
                        Path.GetFileNameWithoutExtension(assembly.TargetPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    OutputMessages.AddErrorMessage("GenerateManifest.IdentityFileNameMismatch", assembly.ToString(), assembly.AssemblyIdentity.Name, assembly.AssemblyIdentity.Name + Path.GetExtension(assembly.TargetPath));
                }
            }
        }

        private void ValidateReferencesForClickOnceApplication()
        {
            int t1 = Environment.TickCount;
            bool isPartialTrust = !TrustInfo.IsFullTrust;
            var targetPathList = new Dictionary<string, NGen<bool>>();

            foreach (AssemblyReference assembly in AssemblyReferences)
            {
                // Check all resolved dependencies for partial trust apps...
                if (isPartialTrust && (assembly != EntryPoint) && !String.IsNullOrEmpty(assembly.ResolvedPath))
                {
                    ValidateReferenceForPartialTrust(assembly, TrustInfo);
                }

                // Check TargetPath for all local dependencies, ignoring any Prerequisites
                if (!assembly.IsPrerequisite && !String.IsNullOrEmpty(assembly.TargetPath))
                {
                    // Check target path does not exceed maximum...
                    if (_maxTargetPath > 0 && assembly.TargetPath.Length > _maxTargetPath)
                    {
                        OutputMessages.AddWarningMessage("GenerateManifest.TargetPathTooLong", assembly.ToString(), _maxTargetPath.ToString(CultureInfo.CurrentCulture));
                    }

                    // Check for two or more items with the same TargetPath...
                    string key = assembly.TargetPath.ToLowerInvariant();
                    if (!targetPathList.ContainsKey(key))
                    {
                        targetPathList.Add(key, false);
                    }
                    else if (targetPathList[key] == false)
                    {
                        OutputMessages.AddWarningMessage("GenerateManifest.DuplicateTargetPath", assembly.ToString());
                        targetPathList[key] = true; // only warn once per path
                    }
                }
                else
                {
                    // Check assembly name does not exceed maximum...
                    if (_maxTargetPath > 0 && assembly.AssemblyIdentity.Name.Length > _maxTargetPath)
                    {
                        OutputMessages.AddWarningMessage("GenerateManifest.TargetPathTooLong", assembly.AssemblyIdentity.Name, _maxTargetPath.ToString(CultureInfo.CurrentCulture));
                    }
                }

                // Check that all prerequisites are strong named...
                if (assembly.IsPrerequisite && !assembly.AssemblyIdentity.IsStrongName && !assembly.IsVirtual)
                {
                    OutputMessages.AddErrorMessage("GenerateManifest.PrerequisiteNotSigned", assembly.ToString());
                }
            }
            foreach (FileReference file in FileReferences)
            {
                // Check that file is not an assembly...
                if (!String.IsNullOrEmpty(file.ResolvedPath) && PathUtil.IsAssembly(file.ResolvedPath))
                {
                    OutputMessages.AddWarningMessage("GenerateManifest.AssemblyAsFile", file.ToString());
                }

                if (!String.IsNullOrEmpty(file.TargetPath))
                {
                    // Check target path does not exceed maximum...
                    if (_maxTargetPath > 0 && file.TargetPath.Length > _maxTargetPath)
                    {
                        OutputMessages.AddWarningMessage("GenerateManifest.TargetPathTooLong", file.TargetPath, _maxTargetPath.ToString(CultureInfo.CurrentCulture));
                    }

                    // Check for two or more items with the same TargetPath...
                    string key = file.TargetPath.ToLowerInvariant();
                    if (!targetPathList.ContainsKey(key))
                    {
                        targetPathList.Add(key, false);
                    }
                    else if (targetPathList[key] == false)
                    {
                        OutputMessages.AddWarningMessage("GenerateManifest.DuplicateTargetPath", file.TargetPath);
                        targetPathList[key] = true; // only warn once per path
                    }
                }
            }
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "GenerateManifest.CheckManifestReferences t={0}", Environment.TickCount - t1));
        }

        private void ValidateReferenceForPartialTrust(AssemblyReference assembly, TrustInfo trustInfo)
        {
            if (trustInfo.IsFullTrust)
            {
                return;
            }
            string path = assembly.ResolvedPath;
            var flags = new AssemblyAttributeFlags(path);

            // if it's targeting v2.0 CLR then use the old logic to check for partial trust callers.
            if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) <= 0)
            {
                if (assembly.IsPrimary && flags.IsSigned
                                       && !flags.HasAllowPartiallyTrustedCallersAttribute)
                {
                    OutputMessages.AddWarningMessage("GenerateManifest.AllowPartiallyTrustedCallers", Path.GetFileNameWithoutExtension(path));
                }
            }
            else
            {
                if (assembly.AssemblyIdentity != null && assembly.AssemblyIdentity.IsInFramework(Constants.DotNetFrameworkIdentifier, TargetFrameworkVersion))
                {
                    // if the binary is targeting v4.0 and it has the transparent attribute then we may allow partially trusted callers.
                    if (assembly.IsPrimary
                        && !(flags.HasAllowPartiallyTrustedCallersAttribute || flags.HasSecurityTransparentAttribute))
                    {
                        OutputMessages.AddWarningMessage("GenerateManifest.AllowPartiallyTrustedCallers", Path.GetFileNameWithoutExtension(path));
                    }
                }
                else
                {
                    // if the binary is targeting v4.0 and it has the transparent attribute then we may allow partially trusted callers.
                    if (assembly.IsPrimary && flags.IsSigned
                                           && !(flags.HasAllowPartiallyTrustedCallersAttribute ||
                                                flags.HasSecurityTransparentAttribute))
                    {
                        OutputMessages.AddWarningMessage("GenerateManifest.AllowPartiallyTrustedCallers", Path.GetFileNameWithoutExtension(path));
                    }
                }
            }

            if (flags.HasPrimaryInteropAssemblyAttribute || flags.HasImportedFromTypeLibAttribute)
            {
                OutputMessages.AddWarningMessage("GenerateManifest.UnmanagedCodePermission", Path.GetFileNameWithoutExtension(path));
            }
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("ConfigFile")]
        public string XmlConfigFile
        {
            get => _configFile;
            set => _configFile = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlElement("EntryPointIdentity")]
        public AssemblyIdentity XmlEntryPointIdentity
        {
            get => _entryPointIdentity;
            set => _entryPointIdentity = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("EntryPointParameters")]
        public string XmlEntryPointParameters
        {
            get => _entryPointParameters;
            set => _entryPointParameters = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("EntryPointPath")]
        public string XmlEntryPointPath
        {
            get => _entryPointPath;
            set => _entryPointPath = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("ErrorReportUrl")]
        public string XmlErrorReportUrl
        {
            get => _errorReportUrl;
            set => _errorReportUrl = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlArray("FileAssociations")]
        public FileAssociation[] XmlFileAssociations
        {
            get => _fileAssociations;
            set => _fileAssociations = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("HostInBrowser")]
        public string XmlHostInBrowser
        {
            get => Convert.ToString(_hostInBrowser, CultureInfo.InvariantCulture).ToLowerInvariant();
            set => _hostInBrowser = ConvertUtil.ToBoolean(value);
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("IconFile")]
        public string XmlIconFile
        {
            get => _iconFile;
            set => _iconFile = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("IsClickOnceManifest")]
        public string XmlIsClickOnceManifest
        {
            get => Convert.ToString(_isClickOnceManifest, CultureInfo.InvariantCulture).ToLowerInvariant();
            set => _isClickOnceManifest = ConvertUtil.ToBoolean(value);
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("OSMajor")]
        public string XmlOSMajor
        {
            get => _oSMajor;
            set => _oSMajor = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("OSMinor")]
        public string XmlOSMinor
        {
            get => _oSMinor;
            set => _oSMinor = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("OSBuild")]
        public string XmlOSBuild
        {
            get => _oSBuild;
            set => _oSBuild = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("OSRevision")]
        public string XmlOSRevision
        {
            get => _oSRevision;
            set => _oSRevision = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("OSSupportUrl")]
        public string XmlOSSupportUrl
        {
            get => _oSSupportUrl;
            set => _oSSupportUrl = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("OSDescription")]
        public string XmlOSDescription
        {
            get => _oSDescription;
            set => _oSDescription = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Product")]
        public string XmlProduct
        {
            get => _product;
            set => _product = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Publisher")]
        public string XmlPublisher
        {
            get => _publisher;
            set => _publisher = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("SuiteName")]
        public string XmlSuiteName
        {
            get => _suiteName;
            set => _suiteName = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("SupportUrl")]
        public string XmlSupportUrl
        {
            get => _supportUrl;
            set => _supportUrl = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("UseApplicationTrust")]
        public string XmlUseApplicationTrust
        {
            get => Convert.ToString(_useApplicationTrust, CultureInfo.InvariantCulture).ToLowerInvariant();
            set => _useApplicationTrust = ConvertUtil.ToBoolean(value);
        }

        #endregion

        #region AssemblyAttributeFlags
        private class AssemblyAttributeFlags
        {
            public readonly bool IsSigned;
            public readonly bool HasAllowPartiallyTrustedCallersAttribute;
            public readonly bool HasPrimaryInteropAssemblyAttribute;
            public readonly bool HasImportedFromTypeLibAttribute;
            public readonly bool HasSecurityTransparentAttribute;

            public AssemblyAttributeFlags(string path)
            {
                using (MetadataReader r = MetadataReader.Create(path))
                    if (r != null)
                    {
                        IsSigned = !String.IsNullOrEmpty(r.PublicKeyToken);
                        HasAllowPartiallyTrustedCallersAttribute = r.HasAssemblyAttribute("System.Security.AllowPartiallyTrustedCallersAttribute");
                        HasSecurityTransparentAttribute = r.HasAssemblyAttribute("System.Security.SecurityTransparentAttribute");
                        HasPrimaryInteropAssemblyAttribute = r.HasAssemblyAttribute("System.Runtime.InteropServices.PrimaryInteropAssemblyAttribute");
                        HasImportedFromTypeLibAttribute = r.HasAssemblyAttribute("System.Runtime.InteropServices.ImportedFromTypeLibAttribute");
                    }
            }
        }
        #endregion
    }
}
