using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public ITaskItem[] RuntimeFrameworksToRemove { get; set; }

        protected override void ExecuteCore()
        {
            List<TaskItem> referencesToAdd = new List<TaskItem>();
            List<TaskItem> platformManifests = new List<TaskItem>();
            PackageConflictPreferredPackages = string.Empty;
            List<TaskItem> packageConflictOverrides = new List<TaskItem>();

            var resolvedTargetingPacks = ResolvedTargetingPacks.ToDictionary(item => item.ItemSpec, StringComparer.OrdinalIgnoreCase);

            foreach (var frameworkReference in FrameworkReferences)
            {
                ITaskItem targetingPack;
                resolvedTargetingPacks.TryGetValue(frameworkReference.ItemSpec, out targetingPack);
                string targetingPackRoot = targetingPack?.GetMetadata("Path");
 
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
                        string[] possibleDllPaths = new[]
                        {
                            Path.Combine(targetingPackRoot, "ref", targetingPackTargetFramework),
                            targetingPackDataPath
                        };

                        string[] possibleManifestPaths = new[]
                        {
                            Path.Combine(targetingPackRoot, "build", targetingPackTargetFramework,
                                targetingPack.GetMetadata(MetadataKeys.PackageName) + ".PlatformManifest.txt"),
                            Path.Combine(targetingPackDataPath, "PlatformManifest.txt"),
                            Path.Combine(targetingPackDataPath,
                                        targetingPack.GetMetadata(MetadataKeys.PackageName) + ".PlatformManifest.txt"),
                        };

                        string targetingPackDllPath = possibleDllPaths.First(path =>
                                    Directory.Exists(path) &&
                                    Directory.GetFiles(path, "*.dll").Any());

                        string platformManifestPath = possibleManifestPaths.FirstOrDefault(File.Exists);

                        string packageOverridesPath = Path.Combine(targetingPackDataPath, "PackageOverrides.txt");

                        foreach (var dll in Directory.GetFiles(targetingPackDllPath, "*.dll"))
                        {
                            var reference = CreateReferenceItem(dll, targetingPack);

                            referencesToAdd.Add(reference);
                        }

                        if (platformManifestPath != null)
                        {
                            platformManifests.Add(new TaskItem(platformManifestPath));
                        }

                        if (File.Exists(packageOverridesPath))
                        {
                            packageConflictOverrides.Add(CreatePackageOverride(targetingPack.GetMetadata("RuntimeFrameworkName"), packageOverridesPath));
                        }

                        if (targetingPack.ItemSpec.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase))
                        {
                            //  Hardcode this for now.  Load this from the targeting pack once we have "real" targeting packs
                            //  https://github.com/dotnet/cli/issues/10581
                            PackageConflictPreferredPackages = "Microsoft.NETCore.App;runtime.linux-x64.Microsoft.NETCore.App;runtime.linux-x64.Microsoft.NETCore.App;runtime.linux-musl-x64.Microsoft.NETCore.App;runtime.linux-musl-x64.Microsoft.NETCore.App;runtime.rhel.6-x64.Microsoft.NETCore.App;runtime.rhel.6-x64.Microsoft.NETCore.App;runtime.osx-x64.Microsoft.NETCore.App;runtime.osx-x64.Microsoft.NETCore.App;runtime.freebsd-x64.Microsoft.NETCore.App;runtime.freebsd-x64.Microsoft.NETCore.App;runtime.win-x86.Microsoft.NETCore.App;runtime.win-x86.Microsoft.NETCore.App;runtime.win-arm.Microsoft.NETCore.App;runtime.win-arm.Microsoft.NETCore.App;runtime.win-arm64.Microsoft.NETCore.App;runtime.win-arm64.Microsoft.NETCore.App;runtime.linux-arm.Microsoft.NETCore.App;runtime.linux-arm.Microsoft.NETCore.App;runtime.linux-arm64.Microsoft.NETCore.App;runtime.linux-arm64.Microsoft.NETCore.App;runtime.tizen.4.0.0-armel.Microsoft.NETCore.App;runtime.tizen.4.0.0-armel.Microsoft.NETCore.App;runtime.tizen.5.0.0-armel.Microsoft.NETCore.App;runtime.tizen.5.0.0-armel.Microsoft.NETCore.App;runtime.win-x64.Microsoft.NETCore.App;runtime.win-x64.Microsoft.NETCore.App";
                        }
                    }
                }
            }

            //  Remove RuntimeFramework items for shared frameworks which weren't referenced
            HashSet<string> frameworkReferenceNames = new HashSet<string>(FrameworkReferences.Select(fr => fr.ItemSpec), StringComparer.OrdinalIgnoreCase);
            RuntimeFrameworksToRemove = RuntimeFrameworks.Where(rf => !frameworkReferenceNames.Contains(rf.GetMetadata(MetadataKeys.FrameworkName)))
                                        .ToArray();

            ReferencesToAdd = referencesToAdd.ToArray();
            PlatformManifests = platformManifests.ToArray();
            PackageConflictOverrides = packageConflictOverrides.ToArray();
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

        private TaskItem CreateReferenceItem(string dll, ITaskItem targetingPack)
        {
            var reference = new TaskItem(dll);

            reference.SetMetadata(MetadataKeys.ExternallyResolved, "true");
            reference.SetMetadata(MetadataKeys.Private, "false");
            reference.SetMetadata("Visible", "false");
            reference.SetMetadata(MetadataKeys.NuGetPackageId, targetingPack.GetMetadata(MetadataKeys.PackageName));
            reference.SetMetadata(MetadataKeys.NuGetPackageVersion, targetingPack.GetMetadata(MetadataKeys.PackageVersion));

            //  TODO: Once we work out what metadata we should use here to display these references grouped under the targeting pack
            //  in solution explorer, set that metadata here.These metadata values are based on what PCLs were using.
            //  https://github.com/dotnet/sdk/issues/2802
            reference.SetMetadata("WinMDFile", "false");
            reference.SetMetadata("ReferenceGroupingDisplayName", targetingPack.ItemSpec);
            reference.SetMetadata("ReferenceGrouping", targetingPack.ItemSpec);
            reference.SetMetadata("ResolvedFrom", "TargetingPack");
            reference.SetMetadata("IsSystemReference", "true");
            
            return reference;
        }
    }
}
