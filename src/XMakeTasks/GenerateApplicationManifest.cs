// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates an application manifest for ClickOnce projects.
    /// </summary>
    public sealed class GenerateApplicationManifest : GenerateManifestBase
    {
        private enum _ManifestType { Native, ClickOnce }

        private string _clrVersion = null;
        private ITaskItem _configFile = null;
        private ITaskItem[] _dependencies = null;
        private string _errorReportUrl = null;
        private ITaskItem[] _files = null;
        private ITaskItem _iconFile = null;
        private ITaskItem[] _isolatedComReferences = null;
        private _ManifestType _manifestType = _ManifestType.ClickOnce;
        private string _osVersion = null;
        private ITaskItem _trustInfoFile = null;
        private ITaskItem[] _fileAssociations = null;
        private bool _hostInBrowser = false;
        private bool _useApplicationTrust = false;
        private string _product = null;
        private string _publisher = null;
        private string _suiteName = null;
        private string _supportUrl = null;
        private string _specifiedManifestType = null;
        private string _targetFrameworkSubset = String.Empty;
        private string _targetFrameworkProfile = String.Empty;
        private bool _requiresMinimumFramework35SP1;

        public string ClrVersion
        {
            get { return _clrVersion; }
            set { _clrVersion = value; }
        }

        public ITaskItem ConfigFile
        {
            get { return _configFile; }
            set { _configFile = value; }
        }

        public ITaskItem[] Dependencies
        {
            get { return _dependencies; }
            set { _dependencies = Util.SortItems(value); }
        }

        public string ErrorReportUrl
        {
            get { return _errorReportUrl; }
            set { _errorReportUrl = value; }
        }

        public ITaskItem[] FileAssociations
        {
            get
            {
                // File associations are only valid when targeting 3.5 or later
                if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                    return null;
                return _fileAssociations;
            }
            set { _fileAssociations = value; }
        }

        public ITaskItem[] Files
        {
            get { return _files; }
            set { _files = Util.SortItems(value); }
        }

        public bool HostInBrowser
        {
            get { return _hostInBrowser; }
            set { _hostInBrowser = value; }
        }

        public ITaskItem IconFile
        {
            get { return _iconFile; }
            set { _iconFile = value; }
        }

        public ITaskItem[] IsolatedComReferences
        {
            get { return _isolatedComReferences; }
            set { _isolatedComReferences = Util.SortItems(value); }
        }

        public string ManifestType
        {
            get { return _specifiedManifestType; }
            set { _specifiedManifestType = value; }
        }

        public string OSVersion
        {
            get { return _osVersion; }
            set { _osVersion = value; }
        }

        public string Product
        {
            get { return _product; }
            set { _product = value; }
        }

        public string Publisher
        {
            get { return _publisher; }
            set { _publisher = value; }
        }

        public bool RequiresMinimumFramework35SP1
        {
            get { return _requiresMinimumFramework35SP1; }
            set { _requiresMinimumFramework35SP1 = value; }
        }

        public string SuiteName
        {
            get { return _suiteName; }
            set { _suiteName = value; }
        }

        public string SupportUrl
        {
            get { return _supportUrl; }
            set { _supportUrl = value; }
        }

        public string TargetFrameworkSubset
        {
            get { return _targetFrameworkSubset; }
            set { _targetFrameworkSubset = value; }
        }

        public string TargetFrameworkProfile
        {
            get { return _targetFrameworkProfile; }
            set { _targetFrameworkProfile = value; }
        }

        public ITaskItem TrustInfoFile
        {
            get { return _trustInfoFile; }
            set { _trustInfoFile = value; }
        }

        public bool UseApplicationTrust
        {
            get
            {
                // Use application trust is only used if targeting v3.5 or later
                if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                    return false;
                return _useApplicationTrust;
            }
            set { _useApplicationTrust = value; }
        }

        protected override Type GetObjectType()
        {
            return typeof(ApplicationManifest);
        }

        protected override bool OnManifestLoaded(Manifest manifest)
        {
            return BuildApplicationManifest(manifest as ApplicationManifest);
        }

        protected override bool OnManifestResolved(Manifest manifest)
        {
            if (UseApplicationTrust)
                return BuildResolvedSettings(manifest as ApplicationManifest);
            return true;
        }

        private bool BuildApplicationManifest(ApplicationManifest manifest)
        {
            if (Dependencies != null)
                foreach (ITaskItem item in Dependencies)
                    AddAssemblyFromItem(item);

            if (Files != null)
                foreach (ITaskItem item in Files)
                    AddFileFromItem(item);

            // Build ClickOnce info...
            manifest.IsClickOnceManifest = _manifestType == _ManifestType.ClickOnce;
            if (manifest.IsClickOnceManifest)
            {
                if (manifest.EntryPoint == null && Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.NoEntryPoint");
                    return false;
                }

                if (!AddClickOnceFiles(manifest))
                    return false;

                if (!AddClickOnceFileAssociations(manifest))
                    return false;
            }

            if (HostInBrowser && Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion30) < 0)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.HostInBrowserInvalidFrameworkVersion");
                return false;
            }

            // Build isolated COM info...
            if (!AddIsolatedComReferences(manifest))
                return false;

            manifest.MaxTargetPath = MaxTargetPath;
            manifest.HostInBrowser = HostInBrowser;
            manifest.UseApplicationTrust = UseApplicationTrust;
            if (UseApplicationTrust && SupportUrl != null)
                manifest.SupportUrl = SupportUrl;
            if (UseApplicationTrust && SuiteName != null)
                manifest.SuiteName = SuiteName;
            if (UseApplicationTrust && ErrorReportUrl != null)
                manifest.ErrorReportUrl = ErrorReportUrl;

            return true;
        }

        private bool AddIsolatedComReferences(ApplicationManifest manifest)
        {
            int t1 = Environment.TickCount;
            bool success = true;
            if (IsolatedComReferences != null)
                foreach (ITaskItem item in IsolatedComReferences)
                {
                    string name = item.GetMetadata("Name");
                    if (String.IsNullOrEmpty(name))
                        name = Path.GetFileName(item.ItemSpec);
                    FileReference file = AddFileFromItem(item);
                    if (!file.ImportComComponent(item.ItemSpec, manifest.OutputMessages, name))
                        success = false;
                }

            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "GenerateApplicationManifest.AddIsolatedComReferences t={0}", Environment.TickCount - t1));
            return success;
        }

        private bool AddClickOnceFileAssociations(ApplicationManifest manifest)
        {
            if (FileAssociations != null)
            {
                foreach (ITaskItem item in FileAssociations)
                {
                    FileAssociation fileAssociation = new FileAssociation();
                    fileAssociation.DefaultIcon = item.GetMetadata("DefaultIcon");
                    fileAssociation.Description = item.GetMetadata("Description");
                    fileAssociation.Extension = item.ItemSpec;
                    fileAssociation.ProgId = item.GetMetadata("Progid");
                    manifest.FileAssociations.Add(fileAssociation);
                }
            }

            return true;
        }

        private bool AddClickOnceFiles(ApplicationManifest manifest)
        {
            int t1 = Environment.TickCount;

            if (ConfigFile != null && !String.IsNullOrEmpty(ConfigFile.ItemSpec))
                manifest.ConfigFile = FindFileFromItem(ConfigFile).TargetPath;

            if (IconFile != null && !String.IsNullOrEmpty(IconFile.ItemSpec))
                manifest.IconFile = FindFileFromItem(IconFile).TargetPath;

            if (TrustInfoFile != null && !String.IsNullOrEmpty(TrustInfoFile.ItemSpec))
            {
                manifest.TrustInfo = new TrustInfo();
                manifest.TrustInfo.Read(TrustInfoFile.ItemSpec);
            }

            if (manifest.TrustInfo == null)
                manifest.TrustInfo = new TrustInfo();

            if (OSVersion != null)
            {
                manifest.OSVersion = _osVersion;
            }

            if (ClrVersion != null)
            {
                AssemblyReference CLRPlatformAssembly = manifest.AssemblyReferences.Find(Constants.CLRPlatformAssemblyName);
                if (CLRPlatformAssembly == null)
                {
                    CLRPlatformAssembly = new AssemblyReference();
                    CLRPlatformAssembly.IsPrerequisite = true;
                    manifest.AssemblyReferences.Add(CLRPlatformAssembly);
                }
                CLRPlatformAssembly.AssemblyIdentity = new AssemblyIdentity(Constants.CLRPlatformAssemblyName, ClrVersion);
            }

            if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion30) == 0)
            {
                EnsureAssemblyReferenceExists(manifest, CreateAssemblyIdentity(Constants.NET30AssemblyIdentity));
            }
            else if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) == 0)
            {
                EnsureAssemblyReferenceExists(manifest, CreateAssemblyIdentity(Constants.NET30AssemblyIdentity));
                EnsureAssemblyReferenceExists(manifest, CreateAssemblyIdentity(Constants.NET35AssemblyIdentity));

                if ((!String.IsNullOrEmpty(TargetFrameworkSubset) && TargetFrameworkSubset.Equals(Constants.ClientFrameworkSubset, StringComparison.OrdinalIgnoreCase)) ||
            (!String.IsNullOrEmpty(TargetFrameworkProfile) && TargetFrameworkProfile.Equals(Constants.ClientFrameworkSubset, StringComparison.OrdinalIgnoreCase)))
                {
                    EnsureAssemblyReferenceExists(manifest, CreateAssemblyIdentity(Constants.NET35ClientAssemblyIdentity));
                }
                else if (RequiresMinimumFramework35SP1)
                {
                    EnsureAssemblyReferenceExists(manifest, CreateAssemblyIdentity(Constants.NET35SP1AssemblyIdentity));
                }
            }

            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "GenerateApplicationManifest.AddClickOnceFiles t={0}", Environment.TickCount - t1));
            return true;
        }

        protected internal override bool ValidateInputs()
        {
            bool valid = base.ValidateInputs();
            if (_specifiedManifestType != null)
            {
                try
                {
                    _manifestType = (_ManifestType)Enum.Parse(typeof(_ManifestType), _specifiedManifestType, true);
                }
                catch (FormatException)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "ManifestType");
                    valid = false;
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "ManifestType");
                    valid = false;
                }
                if (_manifestType == _ManifestType.Native)
                    EntryPoint = null; // EntryPoint is ignored if ManifestType="Native"
            }
            if (ClrVersion != null && !Util.IsValidVersion(ClrVersion, 4))
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "ClrVersion");
                valid = false;
            }
            if (OSVersion != null && !Util.IsValidVersion(OSVersion, 4))
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "OSVersion");
                valid = false;
            }
            if (!Util.IsValidFrameworkVersion(TargetFrameworkVersion) || Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion20) < 0)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "TargetFrameworkVersion");
                valid = false;
            }
            if (_manifestType == _ManifestType.ClickOnce)
            {
                // ClickOnce supports asInvoker privilege only.
                string requestedExecutionLevel;
                if (GetRequestedExecutionLevel(out requestedExecutionLevel) && String.CompareOrdinal(requestedExecutionLevel, Constants.UACAsInvoker) != 0)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidRequestedExecutionLevel", requestedExecutionLevel);
                    valid = false;
                }
            }
            return valid;
        }

        private bool BuildResolvedSettings(ApplicationManifest manifest)
        {
            // Note: if changing the logic in this function, please update the logic in 
            //  GenerateDeploymentManifest.BuildResolvedSettings as well.
            if (Product != null)
                manifest.Product = Product;
            else if (String.IsNullOrEmpty(manifest.Product))
                manifest.Product = Path.GetFileNameWithoutExtension(manifest.AssemblyIdentity.Name);
            Debug.Assert(!String.IsNullOrEmpty(manifest.Product));

            if (Publisher != null)
            {
                manifest.Publisher = Publisher;
            }
            else if (String.IsNullOrEmpty(manifest.Publisher))
            {
                string org = Util.GetRegisteredOrganization();
                if (!String.IsNullOrEmpty(org))
                    manifest.Publisher = org;
                else
                    manifest.Publisher = manifest.Product;
            }
            Debug.Assert(!String.IsNullOrEmpty(manifest.Publisher));

            return true;
        }

        private AssemblyIdentity CreateAssemblyIdentity(string[] values)
        {
            if (values.Length != 5)
                return null;
            return new AssemblyIdentity(values[0], values[1], values[2], values[3], values[4]);
        }

        private void EnsureAssemblyReferenceExists(ApplicationManifest manifest, AssemblyIdentity identity)
        {
            if (manifest.AssemblyReferences.Find(identity) == null)
            {
                AssemblyReference assembly = new AssemblyReference();
                assembly.IsPrerequisite = true;
                assembly.AssemblyIdentity = identity;
                manifest.AssemblyReferences.Add(assembly);
            }
        }

        private bool GetRequestedExecutionLevel(out string requestedExecutionLevel)
        {
            requestedExecutionLevel = Constants.UACAsInvoker;  // For backwards compatibility we assume asInvoker to begin with.

            if (InputManifest == null || String.IsNullOrEmpty(InputManifest.ItemSpec) || String.CompareOrdinal(InputManifest.ItemSpec, "NoManifest") == 0)
            {
                return false;
            }

            try
            {
                using (Stream s = File.Open(InputManifest.ItemSpec, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    XmlDocument document = new XmlDocument();
                    XmlReaderSettings xrSettings = new XmlReaderSettings();
                    xrSettings.DtdProcessing = DtdProcessing.Ignore;
                    using (XmlReader xr = XmlReader.Create(s, xrSettings))
                    {
                        document.Load(xr);

                        //Create an XmlNamespaceManager for resolving namespaces.
                        XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);

                        XmlNode node = (XmlElement)document.SelectSingleNode(XPaths.requestedExecutionLevelPath, nsmgr);
                        if (node != null)
                        {
                            XmlAttribute attr = (XmlAttribute)(node.Attributes.GetNamedItem("level"));
                            if (attr != null)
                                requestedExecutionLevel = attr.Value;
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.ReadInputManifestFailed", InputManifest.ItemSpec, ex.Message);
                return false;
            }
        }
    }
}
