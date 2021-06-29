using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

        public bool NuGetRestoreSupported { get; set; } = true;

        public string NetCoreTargetingPackRoot { get; set; }

        public string ProjectLanguage { get; set; }

        [Output]
        public ITaskItem[] ReferencesToAdd { get; set; }

        [Output]
        public ITaskItem[] AnalyzersToAdd { get; set; }

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
            List<TaskItem> analyzersToAdd = new List<TaskItem>();
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
                        if (targetingPack == null)
                        {
                            Log.LogError(Strings.UnknownFrameworkReference, frameworkReference.ItemSpec);
                        }
                        else
                        {
                            if (NuGetRestoreSupported)
                            {
                                Log.LogError(Strings.TargetingPackNeedsRestore, frameworkReference.ItemSpec);
                            }
                            else
                            {
                                Log.LogError(
                                    Strings.TargetingApphostPackMissingCannotRestore,
                                    "Targeting",
                                    $"{NetCoreTargetingPackRoot}\\{targetingPack.GetMetadata("NuGetPackageId") ?? ""}",
                                    targetingPack.GetMetadata("TargetFramework") ?? "",
                                    targetingPack.GetMetadata("NuGetPackageId") ?? "",
                                    targetingPack.GetMetadata("NuGetPackageVersion") ?? ""
                                    );
                            }
                        }
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

                        AddItemsFromFrameworkList(frameworkListPath, targetingPackRoot, targetingPackDllFolder,
                                                  targetingPack, referencesToAdd, analyzersToAdd);

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
            ReferencesToAdd = deduplicatedReferences.Distinct().ToArray();

            List<TaskItem> deduplicatedAnalyzers = DeduplicateItems(analyzersToAdd);
            AnalyzersToAdd = deduplicatedAnalyzers.Distinct().ToArray();

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
                var reference = CreateItem(dll, targetingPack);

                if (!Path.GetFileName(dll).Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
                {
                    reference.SetMetadata("Facade", "true");
                }

                referencesToAdd.Add(reference);
            }
        }

        private void AddItemsFromFrameworkList(string frameworkListPath, string targetingPackRoot,
            string targetingPackDllFolder,
            ITaskItem targetingPack, List<TaskItem> referenceItems, List<TaskItem> analyzerItems)
        {
            XDocument frameworkListDoc = XDocument.Load(frameworkListPath);

            string profile = targetingPack.GetMetadata("Profile");

            bool usePathElementsInFrameworkListAsFallBack =
                TestFirstFileInFrameworkListUsingAssemblyNameConvention(targetingPackDllFolder, frameworkListDoc);

            foreach (var fileElement in frameworkListDoc.Root.Elements("File"))
            {
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

                string itemType = fileElement.Attribute("Type")?.Value;
                bool isAnalyzer = itemType?.Equals("Analyzer", StringComparison.OrdinalIgnoreCase) ?? false;

                string dllPath = usePathElementsInFrameworkListAsFallBack || isAnalyzer ?
                    Path.Combine(targetingPackRoot, fileElement.Attribute("Path").Value) :
                    GetDllPathViaAssemblyName(targetingPackDllFolder, fileElement);

                var item = CreateItem(dllPath, targetingPack);

                item.SetMetadata("AssemblyVersion", fileElement.Attribute("AssemblyVersion").Value);
                item.SetMetadata("FileVersion", fileElement.Attribute("FileVersion").Value);
                item.SetMetadata("PublicKeyToken", fileElement.Attribute("PublicKeyToken").Value);

                if (isAnalyzer)
                {
                    string itemLanguage = fileElement.Attribute("Language")?.Value;

                    if (itemLanguage != null)
                    {
                        // expect cs instead of C#, fs rather than F# per NuGet conventions
                        string projectLanguage = ProjectLanguage?.Replace('#', 's');

                        if (projectLanguage == null || !projectLanguage.Equals(itemLanguage, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    analyzerItems.Add(item);
                }
                else
                {
                    referenceItems.Add(item);
                }
            }
        }

        /// <summary>
        /// Due to https://github.com/dotnet/sdk/issues/12098 we fall back to use "Path" when "AssemblyName" will
        /// not resolve the actual dll.
        /// </summary>
        /// <returns>if use we should use "Path" element in frameworkList as a fallback</returns>
        private static bool TestFirstFileInFrameworkListUsingAssemblyNameConvention(string targetingPackDllFolder,
            XDocument frameworkListDoc)
        {
            bool usePathElementsInFrameworkListPathAsFallBack;
            var firstFileElement = frameworkListDoc.Root.Elements("File").FirstOrDefault();
            if (firstFileElement == null)
            {
                usePathElementsInFrameworkListPathAsFallBack = false;
            }
            else
            {
                string dllPath = GetDllPathViaAssemblyName(targetingPackDllFolder, firstFileElement);

                usePathElementsInFrameworkListPathAsFallBack = !File.Exists(dllPath);
            }

            return usePathElementsInFrameworkListPathAsFallBack;
        }

        private static string GetDllPathViaAssemblyName(string targetingPackDllFolder, XElement fileElement)
        {
            string assemblyName = fileElement.Attribute("AssemblyName").Value;
            var dllPath = Path.Combine(targetingPackDllFolder, assemblyName + ".dll");
            return dllPath;
        }

        private TaskItem CreateItem(string dll, ITaskItem targetingPack)
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
