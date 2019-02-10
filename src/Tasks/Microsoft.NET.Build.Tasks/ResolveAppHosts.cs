using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveAppHosts : TaskBase
    {
        public string TargetFrameworkIdentifier { get; set; }

        public string TargetFrameworkVersion { get; set; }

        public string TargetingPackRoot { get; set; }

        public string AppHostRuntimeIdentifier { get; set; }

        public string RuntimeFrameworkVersion { get; set; }

        public ITaskItem[] PackAsToolShimRuntimeIdentifiers { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// The file name of Apphost asset.
        /// </summary>
        [Required]
        public string DotNetAppHostExecutableNameWithoutExtension { get; set; }

        [Required]
        public string RuntimeGraphPath { get; set; }

        public ITaskItem[] KnownAppHostPacks { get; set; }

        [Output]
        public ITaskItem[] PackagesToDownload { get; set; }

        //  There should only be one AppHost item, but we use an item here so we can attach metadata to it
        //  (ie the apphost pack name and version, and the relative path to the apphost inside of it so
        //  we can resolve the full path later)
        [Output]
        public ITaskItem[] AppHost { get; set; }

        [Output]
        public ITaskItem[] PackAsToolShimAppHostPacks { get; set; }

        protected override void ExecuteCore()
        {
            var normalizedTargetFrameworkVersion = ResolveFrameworkReferences.NormalizeVersion(new Version(TargetFrameworkVersion));

            var knownAppHostPacksForTargetFramework = KnownAppHostPacks
                .Where(appHostPack =>
                {
                    var packTargetFramework = NuGetFramework.Parse(appHostPack.GetMetadata("TargetFramework"));
                    return packTargetFramework.Framework.Equals(TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) &&
                        ResolveFrameworkReferences.NormalizeVersion(packTargetFramework.Version) == normalizedTargetFrameworkVersion;
                })
                .ToList();

            if (knownAppHostPacksForTargetFramework.Count > 1)
            {
                throw new InvalidOperationException("Multiple KnownAppHostPack items applied to the specified target framework, which is not supposed to happen");
            }

            if (knownAppHostPacksForTargetFramework.Count == 0)
            {
                return;
            }

            var packagesToDownload = new List<ITaskItem>();

            if (!string.IsNullOrEmpty(AppHostRuntimeIdentifier))
            {
                var appHostItem = GetAppHostItem(AppHostRuntimeIdentifier, knownAppHostPacksForTargetFramework, packagesToDownload);
                if (appHostItem != null)
                {
                    AppHost = new ITaskItem[] { appHostItem };
                }
            }

            if (PackAsToolShimRuntimeIdentifiers.Length > 0)
            {
                var packAsToolShimAppHostPacks = new List<ITaskItem>();
                foreach (var runtimeIdentifier in PackAsToolShimRuntimeIdentifiers)
                {
                    var appHostItem = GetAppHostItem(runtimeIdentifier.ItemSpec, knownAppHostPacksForTargetFramework, packagesToDownload);
                    if (appHostItem != null)
                    {
                        packAsToolShimAppHostPacks.Add(appHostItem);
                    }
                }
                PackAsToolShimAppHostPacks = packAsToolShimAppHostPacks.ToArray();
            }

            if (packagesToDownload.Any())
            {
                PackagesToDownload = packagesToDownload.ToArray();
            }
        }

        private ITaskItem GetAppHostItem(string runtimeIdentifier, List<ITaskItem> knownAppHostPacksForTargetFramework,
                                         List<ITaskItem> packagesToDownload)
        {
            var selectedAppHostPack = knownAppHostPacksForTargetFramework.Single();

            string appHostRuntimeIdentifiers = selectedAppHostPack.GetMetadata("AppHostRuntimeIdentifiers");
            string appHostPackPattern = selectedAppHostPack.GetMetadata("AppHostPackNamePattern");
            string appHostPackVersion = selectedAppHostPack.GetMetadata("AppHostPackVersion");

            if (!string.IsNullOrEmpty(RuntimeFrameworkVersion))
            {
                appHostPackVersion = RuntimeFrameworkVersion;
            }

            string bestAppHostRuntimeIdentifier = NuGetUtils.GetBestMatchingRid(
                new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath),
                runtimeIdentifier,
                appHostRuntimeIdentifiers.Split(';'),
                out bool wasInGraph);

            if (bestAppHostRuntimeIdentifier == null)
            {
                if (wasInGraph)
                {
                    //  NETSDK1084: There was no app host for available for the specified RuntimeIdentifier '{0}'.
                    Log.LogError(Strings.NoAppHostAvailable, runtimeIdentifier);
                }
                else
                {
                    //  NETSDK1083: The specified RuntimeIdentifier '{0}' is not recognized.
                    Log.LogError(Strings.RuntimeIdentifierNotRecognized, runtimeIdentifier);
                }
                return null;
            }
            else
            {
                string appHostPackName = appHostPackPattern.Replace("**RID**", bestAppHostRuntimeIdentifier);

                string appHostRelativePathInPackage = Path.Combine("runtimes", bestAppHostRuntimeIdentifier, "native",
                    DotNetAppHostExecutableNameWithoutExtension + ExecutableExtension.ForRuntimeIdentifier(bestAppHostRuntimeIdentifier));


                TaskItem appHostItem = new TaskItem("AppHost");
                string appHostPackPath = null;
                if (!string.IsNullOrEmpty(TargetingPackRoot))
                {
                    appHostPackPath = Path.Combine(TargetingPackRoot, appHostPackName, appHostPackVersion);
                }
                if (appHostPackPath != null && Directory.Exists(appHostPackPath))
                {
                    //  Use AppHost from packs folder
                    appHostItem.SetMetadata(MetadataKeys.Path, Path.Combine(appHostPackPath, appHostRelativePathInPackage));
                }
                else
                {
                    //  Download apphost pack
                    TaskItem packageToDownload = new TaskItem(appHostPackName);
                    packageToDownload.SetMetadata(MetadataKeys.Version, appHostPackVersion);

                    packagesToDownload.Add(packageToDownload);

                    appHostItem.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
                    appHostItem.SetMetadata(MetadataKeys.PackageName, appHostPackName);
                    appHostItem.SetMetadata(MetadataKeys.PackageVersion, appHostPackVersion);
                    appHostItem.SetMetadata(MetadataKeys.RelativePath, appHostRelativePathInPackage);
                }

                return appHostItem;
            }
        }
    }
}
