// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Build.Utilities;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Specifies how the application checks for updates.
    /// </summary>
    [ComVisible(false)]
    public enum UpdateMode
    {
        /// <summary>
        /// Check for updates in the background, after the application starts.
        /// </summary>
        Background,
        /// <summary>
        /// Check for updates in the foreground, before the application starts.
        /// </summary>
        Foreground
    }

    /// <summary>
    /// Specifies the units for the update interval.
    /// </summary>
    [ComVisible(false)]
    public enum UpdateUnit
    {
        /// <summary>
        /// Update interval is in hours.
        /// </summary>
        Hours,
        /// <summary>
        /// Update interval is in days.
        /// </summary>
        Days,
        /// <summary>
        /// Update interval is in weeks.
        /// </summary>
        Weeks
    }

    /// <summary>
    /// Describes a ClickOnce deployment manifest.
    /// </summary>
    [ComVisible(false)]
    [XmlRoot("DeployManifest")]
    public sealed class DeployManifest : Manifest
    {
        private string _createDesktopShortcut;
        private string _deploymentUrl;
        private string _disallowUrlActivation;
        private AssemblyReference _entryPoint;
        private string _errorReportUrl;
        private string _install = "true";
        private string _mapFileExtensions;
        private string _minimumRequiredVersion;
        private string _product;
        private string _publisher;
        private string _suiteName;
        private string _supportUrl;
        private string _trustUrlParameters;
        private string _updateEnabled;
        private string _updateInterval = "0";
        private string _updateMode;
        private string _updateUnit = "days";
        private CompatibleFrameworkCollection _compatibleFrameworkList;
        private List<CompatibleFramework> _compatibleFrameworks = new List<CompatibleFramework>();
        private string _targetFrameworkMoniker;

        private const string _redistListFolder = "RedistList";
        private const string _redistListFile = "FrameworkList.xml";

        /// <summary>
        /// Initializes a new instance of the DeployManifest class.
        /// </summary>
        public DeployManifest()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DeployManifest class.
        /// </summary>
        public DeployManifest(string targetFrameworkMoniker)
        {
            DiscoverCompatFrameworks(targetFrameworkMoniker);
        }

        private void DiscoverCompatFrameworks(string moniker)
        {
            if (!string.IsNullOrEmpty(moniker))
            {
                var frameworkName = new FrameworkNameVersioning(moniker);
                if (frameworkName.Version.Major >= 4)
                {
                    _compatibleFrameworks.Clear();
                    DiscoverCompatibleFrameworks(frameworkName);
                }
            }
        }

        private void DiscoverCompatibleFrameworks(FrameworkNameVersioning frameworkName)
        {
            FrameworkNameVersioning installableFrameworkName = GetInstallableFrameworkName(frameworkName);

            // if profile is null or empty.
            if (string.IsNullOrEmpty(installableFrameworkName.Profile))
            {
                _compatibleFrameworks.Add(GetFullCompatFramework(installableFrameworkName));
            }
            else
            {
                _compatibleFrameworks.Add(GetSubsetCompatFramework(installableFrameworkName));
                _compatibleFrameworks.Add(GetFullCompatFramework(installableFrameworkName));
            }
        }

        /// <summary>
        /// codes from GetInstallableFrameworkForTargetFxInternal in 
        /// env/vscore/package/FxMultiTargeting/FrameworkMultiTargetingInternal.cs
        /// </summary>
        private static FrameworkNameVersioning GetInstallableFrameworkName(FrameworkNameVersioning frameworkName)
        {
            string installableFramework = null;
            FrameworkNameVersioning installableFrameworkObj;

            IList<string> referenceAssemblyPaths = GetPathToReferenceAssemblies(frameworkName);

            if (referenceAssemblyPaths != null && referenceAssemblyPaths.Count > 0)
            {
                // the first one in the list is the reference assembly path for the requested TFM
                string referenceAssemblyPath = referenceAssemblyPaths[0];

                // Get the redistlist file path
                string redistListFilePath = GetRedistListFilePath(referenceAssemblyPath);

                if (File.Exists(redistListFilePath))
                {
                    installableFramework = GetInstallableFramework(redistListFilePath);
                }
            }

            // If the installable framework value is not in the redist, there was no redist, or no matching FX we return the sent TFM,  
            // this means frameworks that are installable themselves don't need to specify this property
            // and that all unknown frameworks are assumed to be installable.
            if (installableFramework == null)
            {
                installableFrameworkObj = frameworkName;
            }
            else
            {
                try
                {
                    installableFrameworkObj = new FrameworkNameVersioning(installableFramework);
                }
                catch (ArgumentException)
                {
                    // Redist list data was invalid, behave as if it was not defined.
                    installableFrameworkObj = frameworkName;
                }
            }

            return installableFrameworkObj;
        }

        private static string GetRedistListFilePath(string referenceAssemblyPath)
        {
            string redistListPath = Path.Combine(referenceAssemblyPath, _redistListFolder);
            redistListPath = Path.Combine(redistListPath, _redistListFile);

            return redistListPath;
        }

        private static IList<string> GetPathToReferenceAssemblies(FrameworkNameVersioning targetFrameworkMoniker)
        {
            IList<string> targetFrameworkPaths = null;
            try
            {
                targetFrameworkPaths = ToolLocationHelper.GetPathToReferenceAssemblies(targetFrameworkMoniker);

                // this returns the chained reference assemblies folders of the framework
                // ordered from highest to lowest version
            }
            catch (InvalidOperationException)
            {
                // The chained dirs does not exist
                // or could not read redistlist for chain
            }

            return targetFrameworkPaths;
        }

        /// <summary>
        /// Gets the InstallableFramework by reading the 'InstallableFramework' attribute in the redist file of the target framework
        /// </summary>
        /// <param name="redistListFilePath">the path to the redistlist file</param>
        /// <returns>InstallableFramework</returns>
        private static string GetInstallableFramework(string redistListFilePath)
        {
            string installableFramework = null;

            try
            {
                var doc = new XmlDocument();
                var xrSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (XmlReader xr = XmlReader.Create(redistListFilePath, xrSettings))
                {
                    doc.Load(xr);
                    XmlNode fileListNode = doc.DocumentElement;
                    XmlAttribute nameattr = fileListNode?.Attributes["InstallableFramework"];
                    if (!String.IsNullOrEmpty(nameattr?.Value))
                    {
                        installableFramework = nameattr.Value;
                    }
                }
            }
            catch (Exception)
            {
            }

            return installableFramework;
        }


        private static CompatibleFramework GetSubsetCompatFramework(FrameworkNameVersioning frameworkName)
        {
            CompatibleFramework compat = GetFullCompatFramework(frameworkName);
            compat.Profile = frameworkName.Profile;

            return compat;
        }

        private static CompatibleFramework GetFullCompatFramework(FrameworkNameVersioning frameworkName)
        {
            var compat = new CompatibleFramework
            {
                Version = frameworkName.Version.ToString(),
                SupportedRuntime = PatchCLRVersion(Util.GetClrVersion(frameworkName.Version.ToString())),
                Profile = "Full"
            };

            return compat;
        }

        /// <summary>
        /// conver (MajorVersion).(MinorVersion).(Build).(Revision) to (MajorVersion).(MinorVersion).(Build)
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        private static string PatchCLRVersion(string version)
        {
            try
            {
                var ver = new Version(version);
                var result = new Version(ver.Major, ver.Minor, ver.Build);
                return result.ToString();
            }
            catch (ArgumentException)
            {
                //continue
            }
            catch (FormatException)
            {
                //continue
            }
            catch (OverflowException)
            {
                //continue 
            }

            return version;
        }

        /// <summary>
        /// Specifies whether the application install will create a shortcut on the desktop
        /// If True, the installation will create a shortcut to the application on the desktop.
        /// The default is False
        /// If Install is False, this value will be ignored
        /// </summary>
        [XmlIgnore]
        public bool CreateDesktopShortcut
        {
            get => ConvertUtil.ToBoolean(_createDesktopShortcut);
            set => _createDesktopShortcut = (value ? "true" : null);
        }

        /// <summary>
        /// Specifies the target framework moniker of this project.
        /// </summary>
        [XmlIgnore]
        public string TargetFrameworkMoniker
        {
            get => _targetFrameworkMoniker;
            set
            {
                _targetFrameworkMoniker = value;
                DiscoverCompatFrameworks(_targetFrameworkMoniker);
            }
        }

        /// <summary>
        /// A collection of CompatibleFrameworks
        /// </summary>
        [XmlIgnore]
        public CompatibleFrameworkCollection CompatibleFrameworks
        {
            get
            {
                if (_compatibleFrameworkList == null && _compatibleFrameworks != null)
                {
                    _compatibleFrameworkList = new CompatibleFrameworkCollection(_compatibleFrameworks.ToArray());
                }
                return _compatibleFrameworkList;
            }
        }

        /// <summary>
        /// Specifies the update location for the application.
        /// If this input is not specified then no update location will be defined for the application.
        /// However, if application updates are specified then the update location must be specified.
        /// The specified value should be a fully qualified URL or UNC path.
        /// </summary>
        [XmlIgnore]
        public string DeploymentUrl
        {
            get => _deploymentUrl;
            set => _deploymentUrl = value;
        }

        /// <summary>
        /// Specifies whether the application should be blocked from being activated via a URL.
        /// If this option is True then application can only be activated from the user's Start menu.
        /// The default is False.
        /// This option is ignored if the Install property is False.
        /// </summary>
        [XmlIgnore]
        public bool DisallowUrlActivation
        {
            get => ConvertUtil.ToBoolean(_disallowUrlActivation);
            set => _disallowUrlActivation = value ? "true" : null; // NOTE: disallowUrlActivation=false is implied, and Fusion prefers the false case to be unspecified
        }

        [XmlIgnore]
        public override AssemblyReference EntryPoint
        {
            get => _entryPoint;
            set => _entryPoint = value;
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

        /// <summary>
        /// Specifies whether the application is an installed application or an online only application.
        /// If this flag is True the application will be installed on the user's Start menu, and can be removed from the Add/Remove Programs dialog.
        /// If this flag is False then the application is intended for online use from a web page.
        /// The default is True.
        /// </summary>
        [XmlIgnore]
        public bool Install
        {
            get => ConvertUtil.ToBoolean(_install);
            set => _install = Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Specifies whether or not the ".deploy" file extension mapping is used.
        /// If this flag is true then every application file is published with a ".deploy" file extension.
        /// This option is useful for web server security to limit the number of file extensions that need to be unblocked to enable ClickOnce application deployment.
        /// The default is false.
        /// </summary>
        [XmlIgnore]
        public bool MapFileExtensions
        {
            get => ConvertUtil.ToBoolean(_mapFileExtensions);
            set => _mapFileExtensions = value ? "true" : null; // NOTE: mapFileExtensions=false is implied, and Fusion prefers the false case to be unspecified
        }

        /// <summary>
        /// Specifies whether or not the user can skip the update.
        /// If the user has a version less than the minimum required, he or she will not have the option to skip the update.
        /// The default is to have no minimum required version.
        /// This input only applies when Install is True.
        /// </summary>
        [XmlIgnore]
        public string MinimumRequiredVersion
        {
            get => _minimumRequiredVersion;
            set => _minimumRequiredVersion = value;
        }

        internal override void OnAfterLoad()
        {
            base.OnAfterLoad();
            if (_entryPoint == null && AssemblyReferences != null && AssemblyReferences.Count > 0)
            {
                _entryPoint = AssemblyReferences[0];
                _entryPoint.ReferenceType = AssemblyReferenceType.ClickOnceManifest;
            }
        }

        internal override void OnBeforeSave()
        {
            base.OnBeforeSave();
            if (AssemblyIdentity != null && String.IsNullOrEmpty(AssemblyIdentity.PublicKeyToken))
            {
                AssemblyIdentity.PublicKeyToken = "0000000000000000";
            }
        }

        /// <summary>
        /// Specifies the name of the application.
        /// If this input is not specified then the name is inferred from the identity of the generated manifest.
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
        /// If this input is not specified then the name is inferred from the registered user, or the identity of the generated manifest.
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
        /// Specifies whether or not URL query-string parameters should be made available to the application.
        /// The default is False indicating that parameters will not be available to the application.
        /// </summary>
        [XmlIgnore]
        public bool TrustUrlParameters
        {
            get => ConvertUtil.ToBoolean(_trustUrlParameters);
            set => _trustUrlParameters = value ? "true" : null;
            // NOTE: trustUrlParameters=false is implied, and Fusion prefers the false case to be unspecified
        }

        /// <summary>
        /// Indicates whether or not the application is updatable.
        /// The default is False.
        /// This input only applies when Install is True.
        /// </summary>
        [XmlIgnore]
        public bool UpdateEnabled
        {
            get => ConvertUtil.ToBoolean(_updateEnabled);
            set => _updateEnabled = Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Specifies the update interval for the application.
        /// The default is zero.
        /// This input only applies when Install and UpdateEnabled are both True.
        /// </summary>
        [XmlIgnore]
        public int UpdateInterval
        {
            get
            {
                try { return Convert.ToInt32(_updateInterval, CultureInfo.InvariantCulture); }
                catch (ArgumentException) { return 1; }
                catch (FormatException) { return 1; }
            }
            set => _updateInterval = Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Specifies whether updates should be checked in the foreground before starting the application, or in the background as the application is running.
        /// The default is "Background".
        /// This input only applies when Install and UpdateEnabled are both True.
        /// </summary>
        [XmlIgnore]
        public UpdateMode UpdateMode
        {
            get
            {
                try { return (UpdateMode)Enum.Parse(typeof(UpdateMode), _updateMode, true); }
                catch (FormatException) { return UpdateMode.Foreground; }
                catch (ArgumentException) { return UpdateMode.Foreground; }
            }
            set => _updateMode = value.ToString();
        }

        /// <summary>
        /// Specifies the units for UpdateInterval input.
        /// This input only applies when Install and UpdateEnabled are both True.
        /// </summary>
        [XmlIgnore]
        public UpdateUnit UpdateUnit
        {
            get
            {
                try { return (UpdateUnit)Enum.Parse(typeof(UpdateUnit), _updateUnit, true); }
                catch (FormatException) { return UpdateUnit.Days; }
                catch (ArgumentException) { return UpdateUnit.Days; }
            }
            set => _updateUnit = value.ToString();
        }

        public override void Validate()
        {
            base.Validate();
            ValidateDeploymentProvider();
            ValidateMinimumRequiredVersion();
            ValidatePlatform();
            ValidateEntryPoint();
        }

        private void ValidateDeploymentProvider()
        {
            if (!String.IsNullOrEmpty(_deploymentUrl) && PathUtil.IsLocalPath(_deploymentUrl))
            {
                OutputMessages.AddWarningMessage("GenerateManifest.InvalidDeploymentProvider");
            }
        }

        private void ValidateEntryPoint()
        {
            if (_entryPoint != null)
            {
                if (!String.IsNullOrEmpty(_entryPoint.TargetPath) && !_entryPoint.TargetPath.EndsWith(
                        ".manifest",
                        StringComparison.OrdinalIgnoreCase))
                {
                    OutputMessages.AddErrorMessage("GenerateManifest.InvalidEntryPoint", _entryPoint.ToString());
                }

                string manifestPath = _entryPoint.ResolvedPath;
                if (manifestPath == null)
                {
                    manifestPath = Path.Combine(Path.GetDirectoryName(SourcePath), _entryPoint.TargetPath);
                }
                if (File.Exists(manifestPath))
                {
                    ApplicationManifest entryPointManifest = ManifestReader.ReadManifest(manifestPath, false) as ApplicationManifest;
                    if (entryPointManifest != null)
                    {
                        if (Install)
                        {
                            if (entryPointManifest.HostInBrowser)
                            {
                                OutputMessages.AddErrorMessage("GenerateManifest.HostInBrowserNotOnlineOnly");
                            }
                        }
                        else
                        {
                            if (entryPointManifest.FileAssociations != null &&
                                entryPointManifest.FileAssociations.Count > 0)
                            {
                                OutputMessages.AddErrorMessage("GenerateManifest.FileAssociationsNotInstalled");
                            }
                        }
                    }
                }
            }
        }

        private void ValidateMinimumRequiredVersion()
        {
            if (!String.IsNullOrEmpty(_minimumRequiredVersion))
            {
                var v1 = new Version(_minimumRequiredVersion);
                var v2 = new Version(AssemblyIdentity.Version);
                if (v1 > v2)
                {
                    OutputMessages.AddErrorMessage("GenerateManifest.GreaterMinimumRequiredVersion");
                }
            }
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("CreateDesktopShortcut")]
        public string XmlCreateDesktopShortcut
        {
            get => _createDesktopShortcut?.ToLowerInvariant();
            set => _createDesktopShortcut = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlArray("CompatibleFrameworks")]
        public CompatibleFramework[] XmlCompatibleFrameworks
        {
            get => _compatibleFrameworks.Count > 0 ? _compatibleFrameworks.ToArray() : null;
            set => _compatibleFrameworks = new List<CompatibleFramework>(value);
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("DeploymentUrl")]
        public string XmlDeploymentUrl
        {
            get => _deploymentUrl;
            set => _deploymentUrl = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("DisallowUrlActivation")]
        public string XmlDisallowUrlActivation
        {
            get => _disallowUrlActivation?.ToLowerInvariant();
            set => _disallowUrlActivation = value;
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
        [XmlAttribute("Install")]
        public string XmlInstall
        {
            get => !String.IsNullOrEmpty(_install) ? _install.ToLower(CultureInfo.InvariantCulture) : "true";  // NOTE: Install attribute shouldn't be null in the manifest, so specify install="true" by default
            set => _install = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("MapFileExtensions")]
        public string XmlMapFileExtensions
        {
            get => _mapFileExtensions?.ToLowerInvariant();
            set => _mapFileExtensions = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("MinimumRequiredVersion")]
        public string XmlMinimumRequiredVersion
        {
            get => _minimumRequiredVersion;
            set => _minimumRequiredVersion = value;
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
        [XmlAttribute("TrustUrlParameters")]
        public string XmlTrustUrlParameters
        {
            get => _trustUrlParameters?.ToLowerInvariant();
            set => _trustUrlParameters = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("UpdateEnabled")]
        public string XmlUpdateEnabled
        {
            get => _updateEnabled?.ToLower(CultureInfo.InvariantCulture);
            set => _updateEnabled = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("UpdateInterval")]
        public string XmlUpdateInterval
        {
            get => _updateInterval;
            set => _updateInterval = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("UpdateMode")]
        public string XmlUpdateMode
        {
            get => _updateMode;
            set => _updateMode = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("UpdateUnit")]
        public string XmlUpdateUnit
        {
            get => _updateUnit?.ToLower(CultureInfo.InvariantCulture);
            set => _updateUnit = value;
        }

        #endregion
    }
}
