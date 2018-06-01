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

        private ITaskItem[] _dependencies;
        private ITaskItem[] _files;
        private ITaskItem[] _isolatedComReferences;
        private _ManifestType _manifestType = _ManifestType.ClickOnce;
        private ITaskItem[] _fileAssociations;
        private bool _useApplicationTrust;

        public string ClrVersion { get; set; }

        public ITaskItem ConfigFile { get; set; }

        public ITaskItem[] Dependencies
        {
            get => _dependencies;
            set => _dependencies = Util.SortItems(value);
        }

        public string ErrorReportUrl { get; set; }

        public ITaskItem[] FileAssociations
        {
            get
            {
                // File associations are only valid when targeting 3.5 or later
                if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                {
                    return null;
                }
                return _fileAssociations;
            }
            set => _fileAssociations = value;
        }

        public ITaskItem[] Files
        {
            get => _files;
            set => _files = Util.SortItems(value);
        }

        public bool HostInBrowser { get; set; }

        public ITaskItem IconFile { get; set; }

        public ITaskItem[] IsolatedComReferences
        {
            get => _isolatedComReferences;
            set => _isolatedComReferences = Util.SortItems(value);
        }

        public string ManifestType { get; set; }

        public string OSVersion { get; set; }

        public string Product { get; set; }

        public string Publisher { get; set; }

        public bool RequiresMinimumFramework35SP1 { get; set; }

        public string SuiteName { get; set; }

        public string SupportUrl { get; set; }

        public string TargetFrameworkSubset { get; set; } = String.Empty;

        public string TargetFrameworkProfile { get; set; } = String.Empty;

        public ITaskItem TrustInfoFile { get; set; }

        public bool UseApplicationTrust
        {
            get
            {
                // Use application trust is only used if targeting v3.5 or later
                if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                {
                    return false;
                }
                return _useApplicationTrust;
            }
            set => _useApplicationTrust = value;
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
            {
                foreach (ITaskItem item in Dependencies)
                {
                    AddAssemblyFromItem(item);
                }
            }

            if (Files != null)
            {
                foreach (ITaskItem item in Files)
                {
                    AddFileFromItem(item);
                }
            }

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
                {
                    return false;
                }

                if (!AddClickOnceFileAssociations(manifest))
                {
                    return false;
                }
            }

            if (HostInBrowser && Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion30) < 0)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.HostInBrowserInvalidFrameworkVersion");
                return false;
            }

            // Build isolated COM info...
            if (!AddIsolatedComReferences(manifest))
            {
                return false;
            }

            manifest.MaxTargetPath = MaxTargetPath;
            manifest.HostInBrowser = HostInBrowser;
            manifest.UseApplicationTrust = UseApplicationTrust;
            if (UseApplicationTrust && SupportUrl != null)
            {
                manifest.SupportUrl = SupportUrl;
            }

            if (UseApplicationTrust && SuiteName != null)
            {
                manifest.SuiteName = SuiteName;
            }

            if (UseApplicationTrust && ErrorReportUrl != null)
            {
                manifest.ErrorReportUrl = ErrorReportUrl;
            }

            return true;
        }

        private bool AddIsolatedComReferences(ApplicationManifest manifest)
        {
            int t1 = Environment.TickCount;
            bool success = true;
            if (IsolatedComReferences != null)
            {
                foreach (ITaskItem item in IsolatedComReferences)
                {
                    string name = item.GetMetadata("Name");
                    if (String.IsNullOrEmpty(name))
                    {
                        name = Path.GetFileName(item.ItemSpec);
                    }
                    FileReference file = AddFileFromItem(item);
                    if (!file.ImportComComponent(item.ItemSpec, manifest.OutputMessages, name))
                    {
                        success = false;
                    }
                }
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
                    var fileAssociation = new FileAssociation
                    {
                        DefaultIcon = item.GetMetadata("DefaultIcon"),
                        Description = item.GetMetadata("Description"),
                        Extension = item.ItemSpec,
                        ProgId = item.GetMetadata("Progid")
                    };
                    manifest.FileAssociations.Add(fileAssociation);
                }
            }

            return true;
        }

        private bool AddClickOnceFiles(ApplicationManifest manifest)
        {
            int t1 = Environment.TickCount;

            if (!String.IsNullOrEmpty(ConfigFile?.ItemSpec))
            {
                manifest.ConfigFile = FindFileFromItem(ConfigFile).TargetPath;
            }

            if (!String.IsNullOrEmpty(IconFile?.ItemSpec))
            {
                manifest.IconFile = FindFileFromItem(IconFile).TargetPath;
            }

            if (!String.IsNullOrEmpty(TrustInfoFile?.ItemSpec))
            {
                manifest.TrustInfo = new TrustInfo();
                manifest.TrustInfo.Read(TrustInfoFile.ItemSpec);
            }

            if (manifest.TrustInfo == null)
            {
                manifest.TrustInfo = new TrustInfo();
            }

            if (OSVersion != null)
            {
                manifest.OSVersion = OSVersion;
            }

            if (ClrVersion != null)
            {
                AssemblyReference clrPlatformAssembly = manifest.AssemblyReferences.Find(Constants.CLRPlatformAssemblyName);
                if (clrPlatformAssembly == null)
                {
                    clrPlatformAssembly = new AssemblyReference { IsPrerequisite = true };
                    manifest.AssemblyReferences.Add(clrPlatformAssembly);
                }
                clrPlatformAssembly.AssemblyIdentity = new AssemblyIdentity(Constants.CLRPlatformAssemblyName, ClrVersion);
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
            if (ManifestType != null)
            {
                try
                {
                    _manifestType = (_ManifestType)Enum.Parse(typeof(_ManifestType), ManifestType, true);
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
                {
                    EntryPoint = null; // EntryPoint is ignored if ManifestType="Native"
                }
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
                if (GetRequestedExecutionLevel(out string requestedExecutionLevel) && String.CompareOrdinal(requestedExecutionLevel, Constants.UACAsInvoker) != 0)
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
            {
                manifest.Product = Product;
            }
            else if (String.IsNullOrEmpty(manifest.Product))
            {
                manifest.Product = Path.GetFileNameWithoutExtension(manifest.AssemblyIdentity.Name);
            }
            Debug.Assert(!String.IsNullOrEmpty(manifest.Product));

            if (Publisher != null)
            {
                manifest.Publisher = Publisher;
            }
            else if (String.IsNullOrEmpty(manifest.Publisher))
            {
                string org = Util.GetRegisteredOrganization();
                if (!String.IsNullOrEmpty(org))
                {
                    manifest.Publisher = org;
                }
                else
                {
                    manifest.Publisher = manifest.Product;
                }
            }
            Debug.Assert(!String.IsNullOrEmpty(manifest.Publisher));

            return true;
        }

        private static AssemblyIdentity CreateAssemblyIdentity(string[] values)
        {
            if (values.Length != 5)
            {
                return null;
            }
            return new AssemblyIdentity(values[0], values[1], values[2], values[3], values[4]);
        }

        private static void EnsureAssemblyReferenceExists(ApplicationManifest manifest, AssemblyIdentity identity)
        {
            if (manifest.AssemblyReferences.Find(identity) == null)
            {
                var assembly = new AssemblyReference
                {
                    IsPrerequisite = true,
                    AssemblyIdentity = identity
                };
                manifest.AssemblyReferences.Add(assembly);
            }
        }

        private bool GetRequestedExecutionLevel(out string requestedExecutionLevel)
        {
            requestedExecutionLevel = Constants.UACAsInvoker;  // For backwards compatibility we assume asInvoker to begin with.

            if (String.IsNullOrEmpty(InputManifest?.ItemSpec) || String.CompareOrdinal(InputManifest.ItemSpec, "NoManifest") == 0)
            {
                return false;
            }

            try
            {
                using (Stream s = File.Open(InputManifest.ItemSpec, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var document = new XmlDocument();
                    var xrSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                    using (XmlReader xr = XmlReader.Create(s, xrSettings))
                    {
                        document.Load(xr);

                        // Create an XmlNamespaceManager for resolving namespaces.
                        var nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);

                        XmlNode node = (XmlElement)document.SelectSingleNode(XPaths.requestedExecutionLevelPath, nsmgr);
                        if (node != null)
                        {
                            var attr = (XmlAttribute)(node.Attributes.GetNamedItem("level"));
                            if (attr != null)
                            {
                                requestedExecutionLevel = attr.Value;
                            }
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
