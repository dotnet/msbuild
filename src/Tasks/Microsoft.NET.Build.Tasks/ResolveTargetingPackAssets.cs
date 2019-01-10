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
        public ITaskItem[] ResolvedTargetingPacks { get; set; }

        [Output]
        public ITaskItem[] ReferencesToAdd { get; set; }

        [Output]
        public ITaskItem[] PlatformManifests { get; set; }

        [Output]
        public string PackageConflictPreferredPackages { get; set; }

        [Output]
        public ITaskItem[] PackageConflictOverrides { get; set; }

        protected override void ExecuteCore()
        {
            List<TaskItem> referencesToAdd = new List<TaskItem>();
            List<TaskItem> platformManifests = new List<TaskItem>();
            PackageConflictPreferredPackages = string.Empty;
            List<TaskItem> packageConflictOverrides = new List<TaskItem>();

            foreach (var targetingPack in ResolvedTargetingPacks ?? Enumerable.Empty<ITaskItem>())
            {
                string targetingPackRoot = targetingPack.GetMetadata("Path");
                foreach (var dll in Directory.GetFiles(Path.Combine(targetingPackRoot, "ref", "netcoreapp3.0"), "*.dll"))
                {
                    var reference = new TaskItem(dll);
                    
                    reference.SetMetadata(MetadataKeys.ExternallyResolved, "true");
                    reference.SetMetadata(MetadataKeys.Private, "false");

                    //  TODO: Once we work out what metadata we should use here to display these references grouped under the targeting pack
                    //  in solution explorer, set that metadata here.These metadata values are based on what PCLs were using.
                    //  https://github.com/dotnet/sdk/issues/2802
                    reference.SetMetadata("WinMDFile", "false");
                    reference.SetMetadata("ReferenceGroupingDisplayName", targetingPack.ItemSpec);
                    reference.SetMetadata("ReferenceGrouping", targetingPack.ItemSpec);
                    reference.SetMetadata("ResolvedFrom", "TargetingPack");
                    reference.SetMetadata("IsSystemReference", "true");

                    referencesToAdd.Add(reference);
                }

                var platformManifestPath = Path.Combine(targetingPackRoot, "build", "netcoreapp3.0",
                    targetingPack.GetMetadata(MetadataKeys.PackageName) + ".PlatformManifest.txt");

                if (File.Exists(platformManifestPath))
                {
                    platformManifests.Add(new TaskItem(platformManifestPath));
                }

                if (targetingPack.ItemSpec.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase))
                {
                    //  Hardcode this for now.  Load this from the targeting pack once we have "real" targeting packs
                    //  https://github.com/dotnet/cli/issues/10581
                    PackageConflictPreferredPackages = "Microsoft.NETCore.App;runtime.linux-x64.Microsoft.NETCore.App;runtime.linux-x64.Microsoft.NETCore.App;runtime.linux-musl-x64.Microsoft.NETCore.App;runtime.linux-musl-x64.Microsoft.NETCore.App;runtime.rhel.6-x64.Microsoft.NETCore.App;runtime.rhel.6-x64.Microsoft.NETCore.App;runtime.osx-x64.Microsoft.NETCore.App;runtime.osx-x64.Microsoft.NETCore.App;runtime.freebsd-x64.Microsoft.NETCore.App;runtime.freebsd-x64.Microsoft.NETCore.App;runtime.win-x86.Microsoft.NETCore.App;runtime.win-x86.Microsoft.NETCore.App;runtime.win-arm.Microsoft.NETCore.App;runtime.win-arm.Microsoft.NETCore.App;runtime.win-arm64.Microsoft.NETCore.App;runtime.win-arm64.Microsoft.NETCore.App;runtime.linux-arm.Microsoft.NETCore.App;runtime.linux-arm.Microsoft.NETCore.App;runtime.linux-arm64.Microsoft.NETCore.App;runtime.linux-arm64.Microsoft.NETCore.App;runtime.tizen.4.0.0-armel.Microsoft.NETCore.App;runtime.tizen.4.0.0-armel.Microsoft.NETCore.App;runtime.tizen.5.0.0-armel.Microsoft.NETCore.App;runtime.tizen.5.0.0-armel.Microsoft.NETCore.App;runtime.win-x64.Microsoft.NETCore.App;runtime.win-x64.Microsoft.NETCore.App";
                }
            }

            ReferencesToAdd = referencesToAdd.ToArray();
            PlatformManifests = platformManifests.ToArray();
            PackageConflictOverrides = packageConflictOverrides.ToArray();
        }
    }
}
