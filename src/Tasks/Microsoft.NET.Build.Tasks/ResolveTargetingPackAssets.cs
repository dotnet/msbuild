// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using static Microsoft.DotNet.Cli.EnvironmentVariableNames;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveTargetingPackAssets : TaskBase
    {
        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ResolvedTargetingPacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] RuntimeFrameworks { get; set; } = Array.Empty<ITaskItem>();

        public bool GenerateErrorForMissingTargetingPacks { get; set; }

        public bool NuGetRestoreSupported { get; set; } = true;

        public bool DisableTransitiveFrameworkReferenceDownloads { get; set; }

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

        private static readonly bool s_allowCacheLookup = Environment.GetEnvironmentVariable(ALLOW_TARGETING_PACK_CACHING) != "0";

        public ResolveTargetingPackAssets()
        {
        }

        protected override void ExecuteCore()
        {
            StronglyTypedInputs inputs = GetInputs();

            string cacheKey = inputs.CacheKey();

            ResolvedAssetsCacheEntry results;

            if (s_allowCacheLookup &&
                BuildEngine4?.GetRegisteredTaskObject(
                    cacheKey,
                    RegisteredTaskObjectLifetime.AppDomain /* really "until process exit" */)
                  is ResolvedAssetsCacheEntry cacheEntry)
            {
                // NOTE: It's conceivably possible that the user modified the targeting
                //       packs between builds. Since that is extremely rare and the standard
                //       user scenario reads the targeting pack contents over and over without
                //       modification, we're electing not to check for file modification and
                //       returning any cached results that we have.

                results = cacheEntry;
            }
            else
            {
                results = Resolve(inputs, BuildEngine4);

                if (s_allowCacheLookup)
                {
                    BuildEngine4?.RegisterTaskObject(cacheKey, results, RegisteredTaskObjectLifetime.AppDomain, allowEarlyCollection: true);
                }
            }

            foreach (var error in results.Errors)
            {
                Log.LogError(error);
            }

            ReferencesToAdd = results.ReferencesToAdd;
            AnalyzersToAdd = results.AnalyzersToAdd;
            PlatformManifests = results.PlatformManifests;
            PackageConflictOverrides = results.PackageConflictOverrides;
            PackageConflictPreferredPackages = results.PackageConflictPreferredPackages;
            UsedRuntimeFrameworks = results.UsedRuntimeFrameworks;
        }

        internal StronglyTypedInputs GetInputs() => new(
                        FrameworkReferences,
                        ResolvedTargetingPacks,
                        RuntimeFrameworks,
                        GenerateErrorForMissingTargetingPacks,
                        NuGetRestoreSupported,
                        DisableTransitiveFrameworkReferenceDownloads,
                        NetCoreTargetingPackRoot,
                        ProjectLanguage);

        private static ResolvedAssetsCacheEntry Resolve(StronglyTypedInputs inputs, IBuildEngine4 buildEngine)
        {
            List<TaskItem> referencesToAdd = new();
            List<TaskItem> analyzersToAdd = new();
            List<TaskItem> platformManifests = new();
            List<TaskItem> packageConflictOverrides = new();
            List<string> preferredPackages = new();
            List<string> errors = new();

            var resolvedTargetingPacks = inputs.ResolvedTargetingPacks
                .ToDictionary(
                    tp => tp.Name,
                    StringComparer.OrdinalIgnoreCase);

            FrameworkReference[] frameworkReferences = inputs.FrameworkReferences;

            foreach (var frameworkReference in frameworkReferences)
            {
                bool foundTargetingPack = resolvedTargetingPacks.TryGetValue(frameworkReference.Name, out TargetingPack targetingPack);
                string targetingPackRoot = targetingPack?.Path;

                if (string.IsNullOrEmpty(targetingPackRoot) || !Directory.Exists(targetingPackRoot))
                {
                    if (inputs.GenerateErrorForMissingTargetingPacks)
                    {
                        if (!foundTargetingPack)
                        {
                            if (frameworkReference.Name.Equals("Microsoft.Maui.Essentials", StringComparison.OrdinalIgnoreCase))
                            {
                                errors.Add(Strings.UnknownFrameworkReference_MauiEssentials);
                            }
                            else
                            {
                                errors.Add(string.Format(Strings.UnknownFrameworkReference, frameworkReference.Name));
                            }

                        }
                        else
                        {
                            if (inputs.DisableTransitiveFrameworkReferences)
                            {
                                errors.Add(string.Format(Strings.TargetingPackNotRestored_TransitiveDisabled, frameworkReference.Name));
                            }
                            else if (inputs.NuGetRestoreSupported)
                            {
                                errors.Add(string.Format(Strings.TargetingPackNeedsRestore, frameworkReference.Name));
                            }
                            else
                            {
                                errors.Add(string.Format(
                                    Strings.TargetingApphostPackMissingCannotRestore,
                                    "Targeting",
                                    $"{inputs.NetCoreTargetingPackRoot}\\{targetingPack.NuGetPackageId ?? ""}",
                                    targetingPack.TargetFramework ?? "",
                                    targetingPack.NuGetPackageId ?? "",
                                    targetingPack.NuGetPackageVersion ?? ""
                                    ));
                            }
                        }
                    }
                }
                else
                {
                    string targetingPackFormat = targetingPack.Format;

                    if (targetingPackFormat.Equals("NETStandardLegacy", StringComparison.OrdinalIgnoreCase))
                    {
                        AddNetStandardTargetingPackAssets(targetingPack, targetingPackRoot, referencesToAdd);
                    }
                    else
                    {
                        string targetingPackTargetFramework = targetingPack.TargetFramework;
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

                        FrameworkListDefinition definition = new(
                            frameworkListPath,
                            targetingPackRoot,
                            targetingPackDllFolder,
                            targetingPack.Name,
                            targetingPack.Profile,
                            targetingPack.NuGetPackageId,
                            targetingPack.NuGetPackageVersion,
                            inputs.ProjectLanguage);

                        AddItemsFromFrameworkList(definition, buildEngine, referencesToAdd, analyzersToAdd);

                        if (File.Exists(platformManifestPath))
                        {
                            platformManifests.Add(new TaskItem(platformManifestPath));
                        }

                        if (File.Exists(packageOverridesPath))
                        {
                            packageConflictOverrides.Add(CreatePackageOverride(targetingPack.NuGetPackageId, packageOverridesPath));
                        }

                        preferredPackages.AddRange(targetingPack.PackageConflictPreferredPackages.Split(';'));
                    }
                }
            }

            //  Calculate which RuntimeFramework items should actually be used based on framework references
            HashSet<string> frameworkReferenceNames = new(frameworkReferences.Select(fr => fr.Name), StringComparer.OrdinalIgnoreCase);

            //  Filter out duplicate references (which can happen when referencing two different profiles that overlap)
            List<TaskItem> deduplicatedReferences = DeduplicateItems(referencesToAdd);
            List<TaskItem> deduplicatedAnalyzers = DeduplicateItems(analyzersToAdd);

            ResolvedAssetsCacheEntry newCacheEntry = new()
            {
                ReferencesToAdd = deduplicatedReferences.Distinct().ToArray(),
                AnalyzersToAdd = deduplicatedAnalyzers.Distinct().ToArray(),
                PlatformManifests = platformManifests.ToArray(),
                PackageConflictOverrides = packageConflictOverrides.ToArray(),
                PackageConflictPreferredPackages = string.Join(";", preferredPackages),
                UsedRuntimeFrameworks = inputs.RuntimeFrameworks.Where(rf => frameworkReferenceNames.Contains(rf.FrameworkName))
                    .Select(rf => rf.Item)
                    .ToArray(),
                Errors = errors.ToArray(),
            };
            return newCacheEntry;
        }

        //  Get distinct items based on case-insensitive ItemSpec comparison
        private static List<TaskItem> DeduplicateItems(List<TaskItem> items)
        {
            HashSet<string> seenItemSpecs = new(StringComparer.OrdinalIgnoreCase);
            List<TaskItem> deduplicatedItems = new(items.Count);
            foreach (var item in items)
            {
                if (seenItemSpecs.Add(item.ItemSpec))
                {
                    deduplicatedItems.Add(item);
                }
            }
            return deduplicatedItems;
        }

        private static TaskItem CreatePackageOverride(string runtimeFrameworkName, string packageOverridesPath)
        {
            TaskItem packageOverride = new(runtimeFrameworkName);
            packageOverride.SetMetadata("OverriddenPackages", File.ReadAllText(packageOverridesPath));
            return packageOverride;
        }

        private static void AddNetStandardTargetingPackAssets(TargetingPack targetingPack, string targetingPackRoot, List<TaskItem> referencesToAdd)
        {
            string targetingPackTargetFramework = targetingPack.TargetFramework;
            string targetingPackAssetPath = Path.Combine(targetingPackRoot, "build", targetingPackTargetFramework, "ref");

            foreach (var dll in Directory.GetFiles(targetingPackAssetPath, "*.dll"))
            {
                var reference = CreateItem(
                    dll,
                    targetingPack.Name,
                    targetingPack.NuGetPackageId,
                    targetingPack.NuGetPackageVersion);

                if (!Path.GetFileName(dll).Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
                {
                    reference.SetMetadata("Facade", "true");
                }

                referencesToAdd.Add(reference);
            }
        }

        private static void AddItemsFromFrameworkList(FrameworkListDefinition definition, IBuildEngine4 buildEngine4, List<TaskItem> referenceItems, List<TaskItem> analyzerItems)
        {
            string frameworkListKey = definition.CacheKey();

            if (s_allowCacheLookup &&
                buildEngine4?.GetRegisteredTaskObject(
                  frameworkListKey,
                  RegisteredTaskObjectLifetime.AppDomain)
                is FrameworkList cacheEntry)
            {
                // As above, we are not even checking timestamps here
                // and instead assuming that the targeting pack folder
                // is fully immutable.

                analyzerItems.AddRange(cacheEntry.Analyzers);
                referenceItems.AddRange(cacheEntry.References);
            }

            XDocument frameworkListDoc = XDocument.Load(definition.FrameworkListPath);

            string profile = definition.Profile;

            bool usePathElementsInFrameworkListAsFallBack =
                TestFirstFileInFrameworkListUsingAssemblyNameConvention(definition.TargetingPackDllFolder, frameworkListDoc);

            List<TaskItem> referenceItemsFromThisFramework = new();
            List<TaskItem> analyzerItemsFromThisFramework = new();

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

                string assemblyName = fileElement.Attribute("AssemblyName").Value;

                string dllPath = usePathElementsInFrameworkListAsFallBack || isAnalyzer ?
                    Path.Combine(definition.TargetingPackRoot, fileElement.Attribute("Path").Value) :
                    GetDllPathViaAssemblyName(definition.TargetingPackDllFolder, assemblyName);

                var item = CreateItem(dllPath, definition.FrameworkReferenceName, definition.NuGetPackageId, definition.NuGetPackageVersion);

                item.SetMetadata("AssemblyName", assemblyName);
                item.SetMetadata("AssemblyVersion", fileElement.Attribute("AssemblyVersion").Value);
                item.SetMetadata("FileVersion", fileElement.Attribute("FileVersion").Value);
                item.SetMetadata("PublicKeyToken", fileElement.Attribute("PublicKeyToken").Value);

                if (isAnalyzer)
                {
                    string itemLanguage = fileElement.Attribute("Language")?.Value;

                    if (itemLanguage != null)
                    {
                        // expect cs instead of C#, fs rather than F# per NuGet conventions
                        string projectLanguage = definition.ProjectLanguage?.Replace('#', 's');

                        if (projectLanguage == null || !projectLanguage.Equals(itemLanguage, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    analyzerItemsFromThisFramework.Add(item);
                }
                else
                {
                    referenceItemsFromThisFramework.Add(item);
                }
            }

            if (s_allowCacheLookup)
            {
                FrameworkList list = new()
                {
                    Analyzers = analyzerItemsFromThisFramework.ToArray(),
                    References = referenceItemsFromThisFramework.ToArray(),
                };

                buildEngine4?.RegisterTaskObject(frameworkListKey, list, RegisteredTaskObjectLifetime.AppDomain, allowEarlyCollection: true);
            }

            analyzerItems.AddRange(analyzerItemsFromThisFramework);
            referenceItems.AddRange(referenceItemsFromThisFramework);
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
            return GetDllPathViaAssemblyName(targetingPackDllFolder, assemblyName);
        }

        private static string GetDllPathViaAssemblyName(string targetingPackDllFolder, string assemblyName)
        {
            var dllPath = Path.Combine(targetingPackDllFolder, assemblyName + ".dll");
            return dllPath;
        }

        private static TaskItem CreateItem(string dll, ITaskItem targetingPack)
        {
            return CreateItem(
                dll,
                targetingPack.ItemSpec,
                targetingPack.GetMetadata(MetadataKeys.NuGetPackageId),
                targetingPack.GetMetadata(MetadataKeys.NuGetPackageVersion));
        }

        private static TaskItem CreateItem(string dll, string frameworkReferenceName, string nuGetPackageId, string nuGetPackageVersion)
        {
            var reference = new TaskItem(dll);

            reference.SetMetadata(MetadataKeys.ExternallyResolved, "true");
            reference.SetMetadata(MetadataKeys.Private, "false");
            reference.SetMetadata(MetadataKeys.NuGetPackageId, nuGetPackageId);
            reference.SetMetadata(MetadataKeys.NuGetPackageVersion, nuGetPackageVersion);

            reference.SetMetadata("FrameworkReferenceName", frameworkReferenceName);
            reference.SetMetadata("FrameworkReferenceVersion", nuGetPackageVersion);

            return reference;
        }

        internal class StronglyTypedInputs
        {
            public FrameworkReference[] FrameworkReferences { get; private set; }
            public TargetingPack[] ResolvedTargetingPacks { get; private set; }
            public RuntimeFramework[] RuntimeFrameworks { get; private set; }
            public bool GenerateErrorForMissingTargetingPacks { get; private set; }
            public bool NuGetRestoreSupported { get; private set; }
            public bool DisableTransitiveFrameworkReferences { get; private set; }
            public string NetCoreTargetingPackRoot { get; private set; }
            public string ProjectLanguage { get; private set; }

            public StronglyTypedInputs(
                ITaskItem[] frameworkReferences,
                ITaskItem[] resolvedTargetingPacks,
                ITaskItem[] runtimeFrameworks,
                bool generateErrorForMissingTargetingPacks,
                bool nuGetRestoreSupported,
                bool disableTransitiveFrameworkReferences,
                string netCoreTargetingPackRoot,
                string projectLanguage)
            {
                FrameworkReferences = frameworkReferences.Select(fr => new FrameworkReference(fr.ItemSpec)).ToArray();
                ResolvedTargetingPacks = resolvedTargetingPacks.Select(
                    item => new TargetingPack(
                        item.ItemSpec,
                        item.GetMetadata(MetadataKeys.Path),
                        item.GetMetadata("TargetingPackFormat"),
                        item.GetMetadata("TargetFramework"),
                        item.GetMetadata("Profile"),
                        item.GetMetadata(MetadataKeys.NuGetPackageId),
                        item.GetMetadata(MetadataKeys.NuGetPackageVersion),
                        item.GetMetadata(MetadataKeys.PackageConflictPreferredPackages)))
                    .ToArray();
                RuntimeFrameworks = runtimeFrameworks.Select(item => new RuntimeFramework(item.ItemSpec, item.GetMetadata(MetadataKeys.FrameworkName), item)).ToArray();
                GenerateErrorForMissingTargetingPacks = generateErrorForMissingTargetingPacks;
                NuGetRestoreSupported = nuGetRestoreSupported;
                DisableTransitiveFrameworkReferences = disableTransitiveFrameworkReferences;
                NetCoreTargetingPackRoot = netCoreTargetingPackRoot;
                ProjectLanguage = projectLanguage;
            }

            public string CacheKey()
            {
                StringBuilder cacheKeyBuilder = new(nameof(ResolveTargetingPackAssets) + nameof(StronglyTypedInputs));
                cacheKeyBuilder.AppendLine();

                foreach (var frameworkReference in FrameworkReferences)
                {
                    cacheKeyBuilder.AppendLine(frameworkReference.CacheKey());
                }
                cacheKeyBuilder.AppendLine();
                foreach (var resolvedTargetingPack in ResolvedTargetingPacks)
                {
                    cacheKeyBuilder.AppendLine(resolvedTargetingPack.CacheKey());
                }

                cacheKeyBuilder.AppendLine(nameof(RuntimeFrameworks));
                foreach (var runtimeFramework in RuntimeFrameworks)
                {
                    cacheKeyBuilder.AppendLine(runtimeFramework.CacheKey());
                }

                cacheKeyBuilder.AppendLine($"{nameof(GenerateErrorForMissingTargetingPacks)}={GenerateErrorForMissingTargetingPacks}");
                cacheKeyBuilder.AppendLine($"{nameof(NuGetRestoreSupported)}={NuGetRestoreSupported}");
                cacheKeyBuilder.AppendLine($"{nameof(DisableTransitiveFrameworkReferences)}={DisableTransitiveFrameworkReferences}");

                cacheKeyBuilder.AppendLine($"{nameof(NetCoreTargetingPackRoot)}={NetCoreTargetingPackRoot}");

                cacheKeyBuilder.AppendLine($"{nameof(ProjectLanguage)}={ProjectLanguage}");

                return cacheKeyBuilder.ToString();

            }
        }

        private class FrameworkList
        {
            public IReadOnlyList<TaskItem> Analyzers;
            public IReadOnlyList<TaskItem> References;
        }

        internal class FrameworkReference
        {
            public string Name { get; private set; }

            public FrameworkReference(string name)
            {
                Name = name;
            }

            public string CacheKey()
            {
                return $"FrameworkReference: {Name}";
            }
        }

        internal class TargetingPack
        {
            public string Name { get; private set; }
            public string Path { get; private set; }
            public string Format { get; private set; }
            public string TargetFramework { get; private set; }
            public string Profile { get; private set; }
            public string NuGetPackageId { get; private set; }
            public string NuGetPackageVersion { get; private set; }
            public string PackageConflictPreferredPackages { get; private set; }

            public TargetingPack(
                string name,
                string path,
                string format,
                string targetFramework,
                string profile,
                string nuGetPackageId,
                string nuGetPackageVersion,
                string packageConflictPreferredPackages)
            {
                Name = name;
                Path = path;
                Format = format;
                TargetFramework = targetFramework;
                Profile = profile;
                NuGetPackageId = nuGetPackageId;
                NuGetPackageVersion = nuGetPackageVersion;
                PackageConflictPreferredPackages = packageConflictPreferredPackages;
            }

            public string CacheKey()
            {
                StringBuilder builder = new();
                builder.AppendLine(nameof(TargetingPack));

                builder.AppendLine(Name);
                builder.AppendLine(Path);
                builder.AppendLine(Format);
                builder.AppendLine(TargetFramework);
                builder.AppendLine(Profile);
                builder.AppendLine(NuGetPackageId);
                builder.AppendLine(NuGetPackageVersion);
                builder.AppendLine(PackageConflictPreferredPackages);

                return builder.ToString();
            }
        }

        internal class RuntimeFramework
        {
            public string Name { get; private set; }
            public string FrameworkName { get; private set; }
            public readonly ITaskItem Item;

            public RuntimeFramework(string name, string frameworkName, ITaskItem item)
            {
                Name = name;
                FrameworkName = frameworkName;
                Item = item;
            }

            public string CacheKey()
            {
                return $"{nameof(RuntimeFramework)}: {Name} ({FrameworkName} {Item?.GetMetadata(MetadataKeys.Version)})";
            }
        }

        internal readonly struct FrameworkListDefinition
        {
            public readonly string FrameworkListPath;
            public readonly string TargetingPackRoot;
            public readonly string TargetingPackDllFolder;
            public readonly string ProjectLanguage;

            public readonly string FrameworkReferenceName;
            public readonly string Profile;
            public readonly string NuGetPackageId;
            public readonly string NuGetPackageVersion;

            public FrameworkListDefinition(string frameworkListPath,
                                           string targetingPackRoot,
                                           string targetingPackDllFolder,
                                           string frameworkReferenceName,
                                           string profile,
                                           string nuGetPackageId,
                                           string nuGetPackageVersion,
                                           string projectLanguage)
            {
                FrameworkListPath = frameworkListPath;
                TargetingPackRoot = targetingPackRoot;
                TargetingPackDllFolder = targetingPackDllFolder;
                ProjectLanguage = projectLanguage;

                FrameworkReferenceName = frameworkReferenceName;
                Profile = profile;
                NuGetPackageId = nuGetPackageId;
                NuGetPackageVersion = nuGetPackageVersion;
            }

            /// <summary>
            /// Construct a key for the framework-specific cache lookup.
            /// </summary>
            public string CacheKey()
            {
                // IMPORTANT: any input changes that can affect the output should be included in this key.
                StringBuilder keyBuilder = new(nameof(FrameworkListDefinition));
                keyBuilder.AppendLine();
                keyBuilder.AppendLine(FrameworkListPath);
                keyBuilder.AppendLine(TargetingPackRoot);
                keyBuilder.AppendLine(TargetingPackDllFolder);
                keyBuilder.AppendLine(FrameworkReferenceName);
                keyBuilder.AppendLine(Profile);
                keyBuilder.AppendLine(NuGetPackageId);
                keyBuilder.AppendLine(NuGetPackageVersion);
                keyBuilder.AppendLine(ProjectLanguage);

                string frameworkListKey = keyBuilder.ToString();
                return frameworkListKey;
            }
        }

        private class ResolvedAssetsCacheEntry
        {
            public ITaskItem[] ReferencesToAdd;
            public ITaskItem[] AnalyzersToAdd;
            public ITaskItem[] PlatformManifests;
            public string PackageConflictPreferredPackages;
            public ITaskItem[] PackageConflictOverrides;
            public ITaskItem[] UsedRuntimeFrameworks;
            public string[] Errors;
        }
    }
}
