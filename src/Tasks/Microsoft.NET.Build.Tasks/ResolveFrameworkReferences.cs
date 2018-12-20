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
    /// <summary>
    /// This class processes the FrameworkReference items.  It adds PackageReferences for the
    /// targeting packs which provide the reference assemblies, and creates RuntimeFramework
    /// items, which are written to the runtimeconfig file
    /// </summary>
    public class ResolveFrameworkReferences : TaskBase
    {
        public string TargetFrameworkIdentifier { get; set; }

        public string TargetFrameworkVersion { get; set; }

        //public string[] TargetingPackRoots { get; set; } = Array.Empty<string>();
        public string TargetingPackRoot { get; set; }

        //  TODO: Remove this.  For packages to download, set a relative path, and use a task which runs later to
        //  resolve the full path using the packageFolders from the assets file
        public string NuGetPackageRoot { get; set; }

        public string AppHostRuntimeIdentifier { get; set; }

        public string RuntimeGraphPath { get; set; }

        /// <summary>
        /// The file name of Apphost asset.
        /// </summary>
        [Required]
        public string DotNetAppHostExecutableNameWithoutExtension { get; set; }

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownFrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] PackagesToDownload { get; set; }

        [Output]
        public ITaskItem[] RuntimeFrameworks { get; set; }

        [Output]
        public ITaskItem[] TargetingPacks { get; set; }

        [Output]
        public string AppHostPath { get; set; }

        [Output]
        public string[] UnresolvedFrameworkReferences { get; set; }

        protected override void ExecuteCore()
        {
            var knownFrameworkReferences = KnownFrameworkReferences.Select(item => new KnownFrameworkReference(item))
                .Where(kfr => kfr.TargetFramework.Framework.Equals(TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) &&
                              NormalizeVersion(kfr.TargetFramework.Version) == NormalizeVersion(new Version(TargetFrameworkVersion)))
                .ToDictionary(kfr => kfr.Name);

            List<ITaskItem> packagesToDownload = new List<ITaskItem>();
            List<ITaskItem> runtimeFrameworks = new List<ITaskItem>();
            List<ITaskItem> targetingPacks = new List<ITaskItem>();
            List<string> unresolvedFrameworkReferences = new List<string>();

            string appHostPackPattern = null;
            string appHostPackVersion = null;
            string appHostRuntimeIdentifiers = null;

            if (string.IsNullOrEmpty(NuGetPackageRoot))
            {
                //  TODO: Remove this, and just resolve relative paths here, and full paths after reading packageFolders from
                //  assets file
                NuGetPackageRoot = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".nuget", "packages");
            }

            foreach (var frameworkReference in FrameworkReferences)
            {
                KnownFrameworkReference knownFrameworkReference;
                if (knownFrameworkReferences.TryGetValue(frameworkReference.ItemSpec, out knownFrameworkReference))
                {
                    if (!string.IsNullOrEmpty(knownFrameworkReference.AppHostPackNamePattern))
                    {
                        if (appHostPackPattern == null)
                        {
                            appHostPackPattern = knownFrameworkReference.AppHostPackNamePattern;
                            appHostPackVersion = knownFrameworkReference.LatestRuntimeFrameworkVersion;
                            appHostRuntimeIdentifiers = knownFrameworkReference.AppHostRuntimeIdentifiers;
                        }
                        else
                        {
                            //  Should not happen unless there is a bug, so this message probably doesn't need to be localized
                            Log.LogError("Multiple FrameworkReferences defined an AppHostPack, which is not supposed to happen");
                        }
                    }

                    //  Get the path of the targeting pack in the targeting pack root (e.g. dotnet/ref)
                    string targetingPackPath = null;
                    if (!string.IsNullOrEmpty(TargetingPackRoot))
                    {
                        targetingPackPath = GetPackagePath(knownFrameworkReference.TargetingPackName, knownFrameworkReference.TargetingPackVersion,
                            TargetingPackRoot);
                    }
                    if (targetingPackPath == null || !Directory.Exists(targetingPackPath))
                    {
                        //  Use targeting pack in the NuGet packages folder
                        targetingPackPath = GetPackagePath(knownFrameworkReference.TargetingPackName, knownFrameworkReference.TargetingPackVersion,
                            NuGetPackageRoot);

                        //  If it hasn't been downloaded yet, then download it
                        //  Note that if the restore operation is running, then the NuGetPackageRoot may not be set correctly
                        //  (Because it comes from NuGet config and is written to the nuget.g.props file during restore, which
                        //  isn't imported during restore).  This should be OK as long as restore is run separately from the rest
                        //  of the build (which is what is supposed to happen).
                        if (!Directory.Exists(targetingPackPath))
                        {
                            TaskItem packageToDownload = new TaskItem(knownFrameworkReference.TargetingPackName);
                            packageToDownload.SetMetadata(MetadataKeys.Version, knownFrameworkReference.TargetingPackVersion);

                            packagesToDownload.Add(packageToDownload);
                        }
                    }

                    TaskItem targetingPack = new TaskItem(knownFrameworkReference.Name);
                    targetingPack.SetMetadata("Path", targetingPackPath);
                    targetingPacks.Add(targetingPack);

                    TaskItem runtimeFramework = new TaskItem(knownFrameworkReference.RuntimeFrameworkName);

                    //  Use default (non roll-forward) version for now.  Eventually we'll need to add support for rolling
                    //  forward, and for publishing assets from a runtime pack for self-contained apps
                    runtimeFramework.SetMetadata(MetadataKeys.Version, knownFrameworkReference.DefaultRuntimeFrameworkVersion);

                    runtimeFrameworks.Add(runtimeFramework);
                }
                else
                {
                    unresolvedFrameworkReferences.Add(frameworkReference.ItemSpec);
                }
            }

            if (appHostPackPattern != null && AppHostRuntimeIdentifier != null)
            {

                //  Choose AppHost RID as best match of the specified RID
                string bestAppHostRuntimeIdentifier; // = AppHostRuntimeIdentifier;
                if (appHostRuntimeIdentifiers != null && !string.IsNullOrEmpty(RuntimeGraphPath))
                {
                    var runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
                    bestAppHostRuntimeIdentifier = NuGetUtils.GetBestMatchingRid(runtimeGraph,
                        AppHostRuntimeIdentifier,
                        appHostRuntimeIdentifiers.Split(';'));
                }
                else
                {
                    bestAppHostRuntimeIdentifier = AppHostRuntimeIdentifier;
                }

                string appHostPackName = appHostPackPattern.Replace("**RID**", bestAppHostRuntimeIdentifier);
                string appHostPackPath = null;
                if (!string.IsNullOrEmpty(TargetingPackRoot))
                {
                    appHostPackPath = GetPackagePath(appHostPackName, appHostPackVersion, TargetingPackRoot);
                }
                if (appHostPackPath == null || !Directory.Exists(appHostPackPath))
                {
                    //  Use package in NuGet packase folder
                    appHostPackPath = GetPackagePath(appHostPackName, appHostPackVersion, NuGetPackageRoot);
                    if (!Directory.Exists(appHostPackPath))
                    {
                        TaskItem packageToDownload = new TaskItem(appHostPackName);
                        packageToDownload.SetMetadata(MetadataKeys.Version, appHostPackVersion);
                        packagesToDownload.Add(packageToDownload);
                    }
                }

                AppHostPath = Path.Combine(appHostPackPath, "runtimes", bestAppHostRuntimeIdentifier, "native",
                    DotNetAppHostExecutableNameWithoutExtension + ExecutableExtension.ForRuntimeIdentifier(bestAppHostRuntimeIdentifier));
            }

            if (packagesToDownload.Any())
            {
                PackagesToDownload = packagesToDownload.ToArray();
            }

            if (runtimeFrameworks.Any())
            {
                RuntimeFrameworks = runtimeFrameworks.ToArray();
            }

            if (targetingPacks.Any())
            {
                TargetingPacks = targetingPacks.ToArray();
            }

            if (unresolvedFrameworkReferences.Any())
            {
                UnresolvedFrameworkReferences = unresolvedFrameworkReferences.ToArray();
            }
        }

        static string GetPackagePath(string name, string version, string root)
        {
            return Path.Combine(root, name.ToLowerInvariant(), version);
        }

        static Version NormalizeVersion(Version version)
        {
            if (version.Revision == 0)
            {
                if (version.Build == 0)
                {
                    return new Version(version.Major, version.Minor);
                }
                else
                {
                    return new Version(version.Major, version.Minor, version.Build);
                }
            }

            return version;
        }

        class KnownFrameworkReference
        {
            ITaskItem _item;
            public KnownFrameworkReference(ITaskItem item)
            {
                _item = item;
                TargetFramework = NuGetFramework.Parse(item.GetMetadata("TargetFramework"));
            }

            //  The name / itemspec of the FrameworkReference used in the project
            public string Name => _item.ItemSpec;

            //  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
            public string RuntimeFrameworkName => _item.GetMetadata("RuntimeFrameworkName");
            public string DefaultRuntimeFrameworkVersion => _item.GetMetadata("DefaultRuntimeFrameworkVersion");
            public string LatestRuntimeFrameworkVersion => _item.GetMetadata("LatestRuntimeFrameworkVersion");

            //  The ID of the targeting pack NuGet package to reference
            public string TargetingPackName => _item.GetMetadata("TargetingPackName");
            public string TargetingPackVersion => _item.GetMetadata("TargetingPackVersion");

            public string AppHostPackNamePattern => _item.GetMetadata("AppHostPackNamePattern");

            public string AppHostRuntimeIdentifiers => _item.GetMetadata("AppHostRuntimeIdentifiers");

            public NuGetFramework TargetFramework { get; }
        }
    }
}
