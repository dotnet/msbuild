// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Base class for all manifest generation tasks.
    /// </summary>
    public abstract class GenerateManifestBase : Task
    {
        private enum AssemblyType { Unspecified, Managed, Native, Satellite };
        private enum DependencyType { Install };

        private string _processorArchitecture;
        private int _startTime;
        private string _targetFrameworkVersion = Constants.TargetFrameworkVersion20;

        private Manifest _manifest;
        protected abstract bool OnManifestLoaded(Manifest manifest);
        protected abstract bool OnManifestResolved(Manifest manifest);
        protected abstract Type GetObjectType();


        protected GenerateManifestBase() : base(AssemblyResources.PrimaryResources, "MSBuild.")
        {
        }

        public string AssemblyName { get; set; }

        public string AssemblyVersion { get; set; }

        public string Description { get; set; }

        public ITaskItem EntryPoint { get; set; }

        public ITaskItem InputManifest { get; set; }

        public int MaxTargetPath { get; set; }

        [Output]
        public ITaskItem OutputManifest { get; set; }

        public string Platform { get; set; } = null;

        public string TargetCulture { get; set; } = null;

        public string TargetFrameworkVersion
        {
            get
            {
                if (string.IsNullOrEmpty(_targetFrameworkVersion))
                {
                    return Constants.TargetFrameworkVersion35;
                }
                return _targetFrameworkVersion;
            }
            set => _targetFrameworkVersion = value;
        }

        public string TargetFrameworkMoniker { get; set; }

        protected internal AssemblyReference AddAssemblyNameFromItem(ITaskItem item, AssemblyReferenceType referenceType)
        {
            var assembly = new AssemblyReference
            {
                AssemblyIdentity = AssemblyIdentity.FromAssemblyName(item.ItemSpec),
                ReferenceType = referenceType
            };
            _manifest.AssemblyReferences.Add(assembly);
            string hintPath = item.GetMetadata("HintPath");
            if (!String.IsNullOrEmpty(hintPath))
            {
                assembly.SourcePath = hintPath;
            }
            SetItemAttributes(item, assembly);
            return assembly;
        }

        protected internal AssemblyReference AddAssemblyFromItem(ITaskItem item)
        {
            // if the assembly is a no-pia assembly and embed interop is turned on, then we don't write it to the manifest.
            if (IsEmbedInteropEnabledForAssembly(item))
            {
                return null;
            }

            AssemblyReferenceType referenceType;
            AssemblyType assemblyType = GetItemAssemblyType(item);
            switch (assemblyType)
            {
                case AssemblyType.Managed:
                    referenceType = AssemblyReferenceType.ManagedAssembly;
                    break;
                case AssemblyType.Native:
                    referenceType = AssemblyReferenceType.NativeAssembly;
                    break;
                case AssemblyType.Satellite:
                    referenceType = AssemblyReferenceType.ManagedAssembly;
                    break;
                default:
                    referenceType = AssemblyReferenceType.Unspecified;
                    break;
            }

            DependencyType dependencyType = GetItemDependencyType(item);
            AssemblyReference assembly;
            if (dependencyType == DependencyType.Install)
            {
                assembly = _manifest.AssemblyReferences.Add(item.ItemSpec);
                SetItemAttributes(item, assembly);
            }
            else
            {
                AssemblyIdentity identity = AssemblyIdentity.FromAssemblyName(item.ItemSpec);
                // If we interpreted the item as a strong name, then treat it as a Fusion display name...
                if (identity.IsStrongName)
                {
                    assembly = new AssemblyReference { AssemblyIdentity = identity };
                }
                else // otherwise treat it as a file path...
                {
                    assembly = new AssemblyReference(item.ItemSpec);
                }
                _manifest.AssemblyReferences.Add(assembly);
                assembly.IsPrerequisite = true;
            }

            assembly.ReferenceType = referenceType;
            string isPrimary = item.GetMetadata(ItemMetadataNames.isPrimary);
            if (string.Equals(isPrimary, "true", StringComparison.Ordinal))
            {
                assembly.IsPrimary = true;
            }

            return assembly;
        }

        protected internal AssemblyReference AddEntryPointFromItem(ITaskItem item, AssemblyReferenceType referenceType)
        {
            AssemblyReference assembly = _manifest.AssemblyReferences.Add(item.ItemSpec);
            assembly.ReferenceType = referenceType;
            SetItemAttributes(item, assembly);
            return assembly;
        }

        protected internal FileReference AddFileFromItem(ITaskItem item)
        {
            FileReference file = _manifest.FileReferences.Add(item.ItemSpec);
            SetItemAttributes(item, file);
            file.IsDataFile = ConvertUtil.ToBoolean(item.GetMetadata("IsDataFile"));
            return file;
        }

        private AssemblyIdentity CreateAssemblyIdentity(AssemblyIdentity baseIdentity, AssemblyIdentity entryPointIdentity)
        {
            string name = AssemblyName;
            string version = AssemblyVersion;
            string publicKeyToken = "0000000000000000";
            string culture = TargetCulture;

            if (String.IsNullOrEmpty(name))
            {
                if (!String.IsNullOrEmpty(baseIdentity?.Name))
                {
                    name = baseIdentity.Name;
                }
                else if (!String.IsNullOrEmpty(entryPointIdentity?.Name))
                {
                    if (_manifest is DeployManifest)
                    {
                        name = Path.GetFileNameWithoutExtension(entryPointIdentity.Name) + ".application";
                    }
                    else if (_manifest is ApplicationManifest)
                    {
                        name = entryPointIdentity.Name + ".exe";
                    }
                }
            }
            if (String.IsNullOrEmpty(name))
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.NoIdentity");
                return null;
            }

            if (String.IsNullOrEmpty(version))
            {
                if (!String.IsNullOrEmpty(baseIdentity?.Version))
                {
                    version = baseIdentity.Version;
                }
                else if (!String.IsNullOrEmpty(entryPointIdentity?.Version))
                {
                    version = entryPointIdentity.Version;
                }
            }

            if (String.IsNullOrEmpty(version))
            {
                version = "1.0.0.0";
            }

            if (String.IsNullOrEmpty(culture))
            {
                if (!String.IsNullOrEmpty(baseIdentity?.Culture))
                {
                    culture = baseIdentity.Culture;
                }
                else if (!String.IsNullOrEmpty(entryPointIdentity?.Culture))
                {
                    culture = entryPointIdentity.Culture;
                }
            }

            if (String.IsNullOrEmpty(culture)
                || String.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase)
                || String.Equals(culture, "*", StringComparison.OrdinalIgnoreCase))
            {
                culture = "neutral";
            }

            if (String.IsNullOrEmpty(_processorArchitecture))
            {
                if (!String.IsNullOrEmpty(baseIdentity?.ProcessorArchitecture))
                {
                    _processorArchitecture = baseIdentity.ProcessorArchitecture;
                }
                else if (!String.IsNullOrEmpty(entryPointIdentity?.ProcessorArchitecture))
                {
                    _processorArchitecture = entryPointIdentity.ProcessorArchitecture;
                }
            }

            if (String.IsNullOrEmpty(_processorArchitecture))
            {
                _processorArchitecture = "msil";
            }

            // Fixup for non-ClickOnce case...
            if (_manifest is ApplicationManifest)
            {
                var applicationManifest = _manifest as ApplicationManifest;
                if (!applicationManifest.IsClickOnceManifest)
                {
                    // Don't need publicKeyToken attribute for non-ClickOnce case
                    publicKeyToken = null;
                    // Language attribute should be omitted if neutral
                    if (String.Compare(culture, "neutral", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        culture = null;
                    }
                    // WinXP loader doesn't understand "msil"
                    if (String.Compare(_processorArchitecture, "msil", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        _processorArchitecture = null;
                    }
                }
            }

            return new AssemblyIdentity(name, version, publicKeyToken, culture, _processorArchitecture);
        }

        public override bool Execute()
        {
            bool success = true;

            Type manifestType = GetObjectType();
            if (!InitializeManifest(manifestType))
            {
                success = false;
            }

            if (success && !BuildManifest())
            {
                success = false;
            }

            if (_manifest != null)
            {
                _manifest.OutputMessages.LogTaskMessages(this);
                if (_manifest.OutputMessages.ErrorCount > 0)
                {
                    success = false;
                }
            }

            return success;
        }

        private bool BuildManifest()
        {
            if (!OnManifestLoaded(_manifest))
            {
                return false;
            }

            if (!ResolveFiles())
            {
                return false;
            }

            if (!ResolveIdentity())
            {
                return false;
            }

            _manifest.SourcePath = GetOutputPath();

            if (!OnManifestResolved(_manifest))
            {
                return false;
            }

            return WriteManifest();
        }

        protected internal FileReference FindFileFromItem(ITaskItem item)
        {
            string targetPath = item.GetMetadata(ItemMetadataNames.targetPath);
            if (String.IsNullOrEmpty(targetPath))
            {
                targetPath = BaseReference.GetDefaultTargetPath(item.ItemSpec);
            }
            foreach (FileReference file in _manifest.FileReferences)
            {
                if (String.Compare(targetPath, file.TargetPath, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return file;
                }
            }
            return AddFileFromItem(item);
        }

        private string GetDefaultFileName()
        {
            if (_manifest is DeployManifest)
            {
                return _manifest.AssemblyIdentity.Name;
            }
            return _manifest.AssemblyIdentity.Name + ".manifest";
        }

        // Returns assembly type (i.e. "Managed", "Native", or "Satellite") as specified by the item.
        // Returns "Unspecified" if item does not specify the assembly type.
        // Logs a warning if specified assembly type is invalid.
        private AssemblyType GetItemAssemblyType(ITaskItem item)
        {
            string value = item.GetMetadata("AssemblyType");
            if (!String.IsNullOrEmpty(value))
            {
                try
                {
                    return (AssemblyType)Enum.Parse(typeof(AssemblyType), value, true);
                }
                catch (FormatException)
                {
                    Log.LogWarningWithCodeFromResources("GenerateManifest.InvalidItemValue", "AssemblyType", item.ItemSpec);
                }
                catch (ArgumentException)
                {
                    Log.LogWarningWithCodeFromResources("GenerateManifest.InvalidItemValue", "AssemblyType", item.ItemSpec);
                }
            }
            return AssemblyType.Unspecified;
        }

        private static bool IsEmbedInteropEnabledForAssembly(ITaskItem item)
        {
            string value = item.GetMetadata("EmbedInteropTypes");
            bool.TryParse(value, out bool result);
            return result;
        }

        // Returns dependency type (i.e. "Install" or "Prerequisite") as specified by the item.
        // Returns "Install" if item does not specify the dependency type.
        // Logs a warning if specified dependency type is invalid.
        private DependencyType GetItemDependencyType(ITaskItem item)
        {
            string value = item.GetMetadata("DependencyType");
            if (!String.IsNullOrEmpty(value))
            {
                try
                {
                    return (DependencyType)Enum.Parse(typeof(DependencyType), value, true);
                }
                catch (FormatException)
                {
                    Log.LogWarningWithCodeFromResources("GenerateManifest.InvalidItemValue", "DependencyType", item.ItemSpec);
                }
                catch (ArgumentException)
                {
                    Log.LogWarningWithCodeFromResources("GenerateManifest.InvalidItemValue", "DependencyType", item.ItemSpec);
                }
            }
            return DependencyType.Install;
        }

        private string GetOutputPath()
        {
            if (OutputManifest != null)
            {
                return OutputManifest.ItemSpec;
            }
            return GetDefaultFileName();
        }

        private bool InitializeManifest(Type manifestType)
        {
            _startTime = Environment.TickCount;

            if (!ValidateInputs())
            {
                return false;
            }

            if (manifestType == null)
            {
                throw new ArgumentNullException(nameof(manifestType));
            }
            if (String.IsNullOrEmpty(InputManifest?.ItemSpec))
            {
                if (manifestType == typeof(ApplicationManifest))
                {
                    _manifest = new ApplicationManifest(TargetFrameworkVersion);
                }
                else if (manifestType == typeof(DeployManifest))
                {
                    _manifest = new DeployManifest(TargetFrameworkMoniker);
                }
                else
                {
                    throw new ArgumentException(String.Empty /* no message */, nameof(manifestType));
                }
            }
            else
            {
                try
                {
                    _manifest = ManifestReader.ReadManifest(manifestType.Name, InputManifest.ItemSpec, true);
                }
                catch (Exception ex)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.ReadInputManifestFailed", InputManifest.ItemSpec, ex.Message);
                    return false;
                }
            }

            if (manifestType != _manifest.GetType())
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidInputManifest");
                return false;
            }

            if (_manifest is DeployManifest deployManifest)
            {
                if (string.IsNullOrEmpty(deployManifest.TargetFrameworkMoniker))
                {
                    deployManifest.TargetFrameworkMoniker = TargetFrameworkMoniker;
                }
            }
            else if (_manifest is ApplicationManifest applicationManifest)
            {
                if (string.IsNullOrEmpty(applicationManifest.TargetFrameworkVersion))
                {
                    applicationManifest.TargetFrameworkVersion = TargetFrameworkVersion;
                }
            }

            if (!String.IsNullOrEmpty(EntryPoint?.ItemSpec))
            {
                AssemblyReferenceType referenceType = AssemblyReferenceType.Unspecified;
                if (_manifest is DeployManifest)
                {
                    referenceType = AssemblyReferenceType.ClickOnceManifest;
                }

                if (_manifest is ApplicationManifest)
                {
                    referenceType = AssemblyReferenceType.ManagedAssembly;
                }
                _manifest.EntryPoint = AddEntryPointFromItem(EntryPoint, referenceType);
            }

            if (Description != null)
            {
                _manifest.Description = Description;
            }

            return true;
        }

        private bool ResolveFiles()
        {
            int t1 = Environment.TickCount;

            string[] searchPaths = { Directory.GetCurrentDirectory() };
            _manifest.ResolveFiles(searchPaths);
            _manifest.UpdateFileInfo(TargetFrameworkVersion);
            if (_manifest.OutputMessages.ErrorCount > 0)
            {
                return false;
            }

            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "GenerateManifestBase.ResolveFiles t={0}", Environment.TickCount - t1));
            return true;
        }

        private bool ResolveIdentity()
        {
            AssemblyIdentity entryPointIdentity = _manifest.EntryPoint?.AssemblyIdentity;
            _manifest.AssemblyIdentity = CreateAssemblyIdentity(_manifest.AssemblyIdentity, entryPointIdentity);
            return _manifest.AssemblyIdentity != null;
        }

        private void SetItemAttributes(ITaskItem item, BaseReference file)
        {
            string targetPath = item.GetMetadata(ItemMetadataNames.targetPath);
            if (!String.IsNullOrEmpty(targetPath))
            {
                file.TargetPath = targetPath;
            }
            else
            {
                file.TargetPath = Path.IsPathRooted(file.SourcePath) || file.SourcePath.StartsWith("..", StringComparison.Ordinal) ? Path.GetFileName(file.SourcePath) : file.SourcePath;
            }
            file.Group = item.GetMetadata("Group");
            file.IsOptional = !String.IsNullOrEmpty(file.Group);
            if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) >= 0)
            {
                file.IncludeHash = ConvertUtil.ToBoolean(item.GetMetadata("IncludeHash"), true);
            }
        }

        protected internal virtual bool ValidateInputs()
        {
            bool valid = true;
            if (!String.IsNullOrEmpty(AssemblyName) && !Util.IsValidAssemblyName(AssemblyName))
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "AssemblyName");
                valid = false;
            }
            if (!String.IsNullOrEmpty(AssemblyVersion) && !Util.IsValidVersion(AssemblyVersion, 4))
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "AssemblyVersion");
                valid = false;
            }
            if (!String.IsNullOrEmpty(TargetCulture) && !Util.IsValidCulture(TargetCulture))
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "TargetCulture");
                valid = false;
            }
            if (!String.IsNullOrEmpty(Platform))
            {
                _processorArchitecture = Util.PlatformToProcessorArchitecture(Platform);
                if (String.IsNullOrEmpty(_processorArchitecture))
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "Platform");
                    valid = false;
                }
            }
            return valid;
        }

        protected internal virtual bool ValidateOutput()
        {
            _manifest.Validate();
            if (_manifest.OutputMessages.ErrorCount > 0)
            {
                return false;
            }

            // Check length of manifest file name does not exceed maximum...
            if (MaxTargetPath > 0)
            {
                string manifestFileName = Path.GetFileName(OutputManifest.ItemSpec);
                if (manifestFileName.Length > MaxTargetPath)
                {
                    Log.LogWarningWithCodeFromResources("GenerateManifest.TargetPathTooLong", manifestFileName, MaxTargetPath);
                }
            }

            return true;
        }

        private bool WriteManifest()
        {
            if (OutputManifest == null)
            {
                OutputManifest = new TaskItem(GetDefaultFileName());
            }

            if (!ValidateOutput())
            {
                return false;
            }

            int t1 = Environment.TickCount;
            try
            {
                ManifestWriter.WriteManifest(_manifest, OutputManifest.ItemSpec, TargetFrameworkVersion);
            }
            catch (Exception ex)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.WriteOutputManifestFailed", OutputManifest.ItemSpec, ex.Message);
                return false;
            }

            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "GenerateManifestBase.WriteManifest t={0}", Environment.TickCount - t1));
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "Total time to generate manifest '{1}': t={0}", Environment.TickCount - _startTime, Path.GetFileName(OutputManifest.ItemSpec)));
            return true;
        }
    }
}
