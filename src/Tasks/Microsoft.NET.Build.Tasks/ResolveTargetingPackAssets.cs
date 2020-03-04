using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveTargetingPackAssets : TaskBase
    {
        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ResolvedTargetingPacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] RuntimeFrameworks { get; set; } = Array.Empty<ITaskItem>();

        public bool GenerateErrorForMissingTargetingPacks { get; set; }

        [Output]
        public ITaskItem[] ReferencesToAdd { get; set; }

        [Output]
        public ITaskItem[] PlatformManifests { get; set; }

        [Output]
        public string PackageConflictPreferredPackages { get; set; }

        [Output]
        public ITaskItem[] PackageConflictOverrides { get; set; }

        [Output]
        public ITaskItem[] UsedRuntimeFrameworks { get; set; }

        public ResolveTargetingPackAssets()
        {
        }

        protected override void ExecuteCore()
        {
            List<TaskItem> referencesToAdd = new List<TaskItem>();
            List<TaskItem> platformManifests = new List<TaskItem>();
            PackageConflictPreferredPackages = string.Empty;
            List<TaskItem> packageConflictOverrides = new List<TaskItem>();
            List<string> preferredPackages = new List<string>();

            var resolvedTargetingPacks = ResolvedTargetingPacks.ToDictionary(item => item.ItemSpec, StringComparer.OrdinalIgnoreCase);

            foreach (var frameworkReference in FrameworkReferences)
            {
                ITaskItem targetingPack;
                resolvedTargetingPacks.TryGetValue(frameworkReference.ItemSpec, out targetingPack);
                string targetingPackRoot = targetingPack?.GetMetadata(MetadataKeys.Path);
 
                if (string.IsNullOrEmpty(targetingPackRoot) || !Directory.Exists(targetingPackRoot))
                {
                    if (GenerateErrorForMissingTargetingPacks)
                    {
                        Log.LogError(Strings.UnknownFrameworkReference, frameworkReference.ItemSpec);
                    }
                }
                else
                {
                    string targetingPackFormat = targetingPack.GetMetadata("TargetingPackFormat");

                    if (targetingPackFormat.Equals("NETStandardLegacy", StringComparison.OrdinalIgnoreCase))
                    {
                        AddNetStandardTargetingPackAssets(targetingPack, targetingPackRoot, referencesToAdd);
                    }
                    else
                    {
                        string targetingPackTargetFramework = targetingPack.GetMetadata("TargetFramework");
                        if (string.IsNullOrEmpty(targetingPackTargetFramework))
                        {
                            targetingPackTargetFramework = "netcoreapp3.0";
                        }

                        string targetingPackDataPath = Path.Combine(targetingPackRoot, "data");

                        string targetingPackDllFolder = Path.Combine(targetingPackRoot, "ref", targetingPackTargetFramework);
                        
                        //  Fall back to netcoreapp5.0 folder if looking for net5.0 and it's not found
                        if (!Directory.Exists(targetingPackDllFolder) &&
                            targetingPackTargetFramework.Equals("net5.0", StringComparison.OrdinalIgnoreCase))
                        {
                            targetingPackTargetFramework = "netcoreapp5.0";
                            targetingPackDllFolder = Path.Combine(targetingPackRoot, "ref", targetingPackTargetFramework);
                        }

                        string platformManifestPath = Path.Combine(targetingPackDataPath, "PlatformManifest.txt");

                        string packageOverridesPath = Path.Combine(targetingPackDataPath, "PackageOverrides.txt");

                        string frameworkListPath = Path.Combine(targetingPackDataPath, "FrameworkList.xml");

                        AddReferencesFromFrameworkList(frameworkListPath, targetingPackDllFolder,
                                                        targetingPack, referencesToAdd);

                        if (File.Exists(platformManifestPath))
                        {
                            platformManifests.Add(new TaskItem(platformManifestPath));
                        }

                        if (File.Exists(packageOverridesPath))
                        {
                            packageConflictOverrides.Add(CreatePackageOverride(targetingPack.GetMetadata(MetadataKeys.NuGetPackageId), packageOverridesPath));
                        }

                        preferredPackages.AddRange(targetingPack.GetMetadata(MetadataKeys.PackageConflictPreferredPackages).Split(';'));
                    }
                }
            }

            //  Calculate which RuntimeFramework items should actually be used based on framework references
            HashSet<string> frameworkReferenceNames = new HashSet<string>(FrameworkReferences.Select(fr => fr.ItemSpec), StringComparer.OrdinalIgnoreCase);
            UsedRuntimeFrameworks = RuntimeFrameworks.Where(rf => frameworkReferenceNames.Contains(rf.GetMetadata(MetadataKeys.FrameworkName)))
                                    .ToArray();

            //  Filter out duplicate references (which can happen when referencing two different profiles that overlap)
            List<TaskItem> deduplicatedReferences = DeduplicateItems(referencesToAdd);
            ReferencesToAdd = deduplicatedReferences.Distinct() .ToArray();

            PlatformManifests = platformManifests.ToArray();
            PackageConflictOverrides = packageConflictOverrides.ToArray();
            PackageConflictPreferredPackages = string.Join(";", preferredPackages);
        }

        //  Get distinct items based on case-insensitive ItemSpec comparison
        private static List<TaskItem> DeduplicateItems(List<TaskItem> items)
        {
            HashSet<string> seenItemSpecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<TaskItem> deduplicatedItems = new List<TaskItem>(items.Count);
            foreach (var item in items)
            {
                if (seenItemSpecs.Add(item.ItemSpec))
                {
                    deduplicatedItems.Add(item);
                }
            }
            return deduplicatedItems;
        }

        private TaskItem CreatePackageOverride(string runtimeFrameworkName, string packageOverridesPath)
        {
            TaskItem packageOverride = new TaskItem(runtimeFrameworkName);
            packageOverride.SetMetadata("OverriddenPackages", File.ReadAllText(packageOverridesPath));
            return packageOverride;
        }

        private void AddNetStandardTargetingPackAssets(ITaskItem targetingPack, string targetingPackRoot, List<TaskItem> referencesToAdd)
        {
            string targetingPackTargetFramework = targetingPack.GetMetadata("TargetFramework");
            string targetingPackAssetPath = Path.Combine(targetingPackRoot, "build", targetingPackTargetFramework, "ref");

            foreach (var dll in Directory.GetFiles(targetingPackAssetPath, "*.dll"))
            {
                var reference = CreateReferenceItem(dll, targetingPack);

                if (!Path.GetFileName(dll).Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
                {
                    reference.SetMetadata("Facade", "true");
                }

                referencesToAdd.Add(reference);
            }
        }

        private void AddReferencesFromFrameworkList(string frameworkListPath, string targetingPackDllFolder,
            ITaskItem targetingPack, List<TaskItem> referenceItems)
        {
            XDocument frameworkListDoc = XDocument.Load(frameworkListPath);

            string profile = targetingPack.GetMetadata("Profile");

            foreach (var fileElement in frameworkListDoc.Root.Elements("File"))
            {
                string assemblyName = fileElement.Attribute("AssemblyName").Value;

                if (!string.IsNullOrEmpty(profile))
                {
                    var profileAttributeValue = fileElement.Attribute("Profile")?.Value;

                    if (profileAttributeValue == null)
                    {
                        //  If profile was specified but this assembly doesn't belong to any profiles, don't reference it
                        continue;
                    }

                    var assemblyProfiles = profileAttributeValue.Split(';');
                    if (!assemblyProfiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
                    {
                        //  Assembly wasn't in profile specified, so don't reference it
                        continue;
                    }
                }

                string referencedByDefaultAttributeValue = fileElement.Attribute("ReferencedByDefault")?.Value;
                if (referencedByDefaultAttributeValue != null &&
                    referencedByDefaultAttributeValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    //  Don't automatically reference this assembly if it has ReferencedByDefault="false"
                    continue;
                }

                var dllPath = Path.Combine(targetingPackDllFolder, assemblyName + ".dll");
                var referenceItem = CreateReferenceItem(dllPath, targetingPack);

                referenceItem.SetMetadata("AssemblyVersion", fileElement.Attribute("AssemblyVersion").Value);
                referenceItem.SetMetadata("FileVersion", fileElement.Attribute("FileVersion").Value);
                referenceItem.SetMetadata("PublicKeyToken", fileElement.Attribute("PublicKeyToken").Value);

                referenceItems.Add(referenceItem);
            }
        }

        private TaskItem CreateReferenceItem(string dll, ITaskItem targetingPack)
        {
            var reference = new TaskItem(dll);

            reference.SetMetadata(MetadataKeys.ExternallyResolved, "true");
            reference.SetMetadata(MetadataKeys.Private, "false");
            reference.SetMetadata(MetadataKeys.NuGetPackageId, targetingPack.GetMetadata(MetadataKeys.NuGetPackageId));
            reference.SetMetadata(MetadataKeys.NuGetPackageVersion, targetingPack.GetMetadata(MetadataKeys.NuGetPackageVersion));

            reference.SetMetadata("FrameworkReferenceName", targetingPack.ItemSpec);
            reference.SetMetadata("FrameworkReferenceVersion", targetingPack.GetMetadata(MetadataKeys.NuGetPackageVersion));
            
            return reference;
        }
    }
}
