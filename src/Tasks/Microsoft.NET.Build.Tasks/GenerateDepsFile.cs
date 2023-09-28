// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).deps.json file.
    /// </summary>
    public class GenerateDepsFile : TaskWithAssemblyResolveHooks
    {
        [Required]
        public string ProjectPath { get; set; }

        public string AssetsFilePath { get; set; }

        [Required]
        public string DepsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        public ITaskItem[] RuntimeFrameworks { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string AssemblyExtension { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        public ITaskItem[] AssemblySatelliteAssemblies { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public bool IncludeMainProject { get; set; }

        // @(ReferencePath) that will be passed to
        public ITaskItem[] ReferencePaths { get; set; } = Array.Empty<ITaskItem>();

        // Full set of @(ReferenceDependencyPaths) found by RAR
        public ITaskItem[] ReferenceDependencyPaths { get; set; } = Array.Empty<ITaskItem>();

        // Full set of @(ReferenceSatellitePaths) found by RAR
        public ITaskItem[] ReferenceSatellitePaths { get; set; } = Array.Empty<ITaskItem>();

        // Subset of @(ReferencePath) that is not CopyLocal, used for compilation, but not runtime assets
        public ITaskItem[] ReferenceAssemblies { get; set; } = Array.Empty<ITaskItem>();

        // Runtime assets for self-contained deployment from runtime pack
        public ITaskItem[] RuntimePackAssets { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem CompilerOptions { get; set; }

        public ITaskItem[] RuntimeStorePackages { get; set; }

        // NuGet compilation assets
        [Required]
        public ITaskItem[] CompileReferences { get; set; }

        // NuGet runtime assets for root directory: @(NativeCopyLocalItems), @(ResourceCopyLocalItems), @(RuntimeCopyLocalItems)
        [Required]
        public ITaskItem[] ResolvedNuGetFiles { get; set; }

        // NuGet runtime assets for runtimes* directory
        [Required]
        public ITaskItem[] ResolvedRuntimeTargetsFiles { get; set; }

        // CopyLocal subset ot of @(ReferencePath), @(ReferenceDependencyPath)
        // Used to filter out non-runtime assemblies from deps file. Only project and direct references in this
        // set will be written to deps file as runtime dependencies.
        public string[] UserRuntimeAssemblies { get; set; }

        public bool IsSelfContained { get; set; }

        public bool IsSingleFile { get; set; }

        public bool IncludeRuntimeFileVersions { get; set; }

        public bool IncludeProjectsNotInAssetsFile { get; set; }

        // List of runtime identifer (platform part only) to validate for runtime assets
        // If set, the task will warn on any RIDs that aren't in the list
        public string[] ValidRuntimeIdentifierPlatformsForAssets { get; set; }

        [Required]
        public string RuntimeGraphPath { get; set; }

        List<ITaskItem> _filesWritten = new();

        [Output]
        public ITaskItem[] FilesWritten
        {
            get { return _filesWritten.ToArray(); }
        }

        private Dictionary<PackageIdentity, string> GetFilteredPackages()
        {
            Dictionary<PackageIdentity, string> filteredPackages = null;

            if (RuntimeStorePackages != null && RuntimeStorePackages.Length > 0)
            {
                filteredPackages = new Dictionary<PackageIdentity, string>();
                foreach (var package in RuntimeStorePackages)
                {
                    filteredPackages.Add(
                        ItemUtilities.GetPackageIdentity(package),
                        package.GetMetadata(MetadataKeys.RuntimeStoreManifestNames));
                }
            }

            return filteredPackages;
        }

        private void WriteDepsFile(string depsFilePath)
        {
            ProjectContext projectContext = null;
            LockFileLookup lockFileLookup = null;
            if (AssetsFilePath != null)
            {
                LockFile lockFile = new LockFileCache(this).GetLockFile(AssetsFilePath);
                projectContext = lockFile.CreateProjectContext(
                    TargetFramework,
                    RuntimeIdentifier,
                    PlatformLibraryName,
                    RuntimeFrameworks,
                    IsSelfContained);

                lockFileLookup = new LockFileLookup(lockFile);
            }

            CompilationOptions compilationOptions = CompilationOptionsConverter.ConvertFrom(CompilerOptions);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                ProjectPath,
                AssemblyName,
                AssemblyExtension,
                AssemblyVersion,
                AssemblySatelliteAssemblies);

            var userRuntimeAssemblySet = new HashSet<string>(UserRuntimeAssemblies ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            Func<ITaskItem, bool> isUserRuntimeAssembly = item => userRuntimeAssemblySet.Contains(item.ItemSpec);

            IEnumerable<ReferenceInfo> referenceAssemblyInfos =
                ReferenceInfo.CreateReferenceInfos(ReferenceAssemblies);

            // If there is a generated asset file, the projectContext will contain most of the project references.
            // So remove any project reference contained within projectContext from directReferences to avoid duplication
            IEnumerable<ReferenceInfo> directReferences =
                ReferenceInfo.CreateDirectReferenceInfos(
                    ReferencePaths,
                    ReferenceSatellitePaths,
                    lockFileLookup,
                    isUserRuntimeAssembly,
                    IncludeProjectsNotInAssetsFile);

            IEnumerable<ReferenceInfo> dependencyReferences =
                ReferenceInfo.CreateDependencyReferenceInfos(ReferenceDependencyPaths, ReferenceSatellitePaths, isUserRuntimeAssembly);

            Dictionary<string, SingleProjectInfo> referenceProjects =
                SingleProjectInfo.CreateProjectReferenceInfos(ReferencePaths, ReferenceSatellitePaths,
                    isUserRuntimeAssembly);

            bool ShouldIncludeRuntimeAsset(ITaskItem item)
            {
                if (IsSelfContained)
                {
                    if (!IsSingleFile || !item.GetMetadata(MetadataKeys.DropFromSingleFile).Equals("true"))
                    {
                        return true;
                    }
                }
                else if (item.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true"))
                {
                    return true;
                }

                return false;
            }

            IEnumerable<RuntimePackAssetInfo> runtimePackAssets =
                RuntimePackAssets.Where(ShouldIncludeRuntimeAsset).Select(RuntimePackAssetInfo.FromItem);

            DependencyContextBuilder builder;
            if (projectContext != null)
            {
                // Generate the RID-fallback for self-contained builds.
                //
                // In order to support loading components with RID-specific assets, 
                // the AssemblyDependencyResolver requires a RID fallback graph.
                // The component itself should not carry the RID fallback graph with it, because
                // it would need to carry graph of all the RIDs and needs updates for newer RIDs.
                // For framework dependent apps, the RID fallback graph comes from the core framework Microsoft.NETCore.App, 
                // so there is no need to write it into the app.
                // If self-contained apps, the (applicable subset of) RID fallback graph needs to be written to the deps.json manifest.
                //
                // If a RID-graph is provided to the DependencyContextBuilder, it generates a RID-fallback 
                // graph with respect to the target RuntimeIdentifier.

                RuntimeGraph runtimeGraph =
                    IsSelfContained ? new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath) : null;

                builder = new DependencyContextBuilder(mainProject, IncludeRuntimeFileVersions, runtimeGraph, projectContext, lockFileLookup);
            }
            else
            {
                builder = new DependencyContextBuilder(
                    mainProject,
                    IncludeRuntimeFileVersions,
                    RuntimeFrameworks,
                    isSelfContained: IsSelfContained,
                    platformLibraryName: PlatformLibraryName,
                    runtimeIdentifier: RuntimeIdentifier,
                    targetFramework: TargetFramework);
            }

            builder = builder
                .WithMainProjectInDepsFile(IncludeMainProject)
                .WithReferenceAssemblies(referenceAssemblyInfos)
                .WithDirectReferences(directReferences)
                .WithDependencyReferences(dependencyReferences)
                .WithReferenceProjectInfos(referenceProjects)
                .WithRuntimePackAssets(runtimePackAssets)
                .WithCompilationOptions(compilationOptions)
                .WithReferenceAssembliesPath(FrameworkReferenceResolver.GetDefaultReferenceAssembliesPath())
                .WithPackagesThatWereFiltered(GetFilteredPackages());

            if (CompileReferences.Length > 0)
            {
                builder = builder.WithCompileReferences(ReferenceInfo.CreateReferenceInfos(CompileReferences));
            }

            var resolvedNuGetFiles = ResolvedNuGetFiles.Select(f => new ResolvedFile(f, false))
                                .Concat(ResolvedRuntimeTargetsFiles.Select(f => new ResolvedFile(f, true)));
            builder = builder.WithResolvedNuGetFiles(resolvedNuGetFiles);

            DependencyContext dependencyContext = builder.Build();

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(depsFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
            _filesWritten.Add(new TaskItem(depsFilePath));

            if (ValidRuntimeIdentifierPlatformsForAssets != null)
            {
                var affectedLibs = new List<string>();
                var affectedRids = new List<string>();
                foreach (var lib in dependencyContext.RuntimeLibraries)
                {
                    var warnOnRids = lib.RuntimeAssemblyGroups.Select(g => g.Runtime).Where(ShouldWarnOnRuntimeIdentifer)
                        .Concat(lib.NativeLibraryGroups.Select(g => g.Runtime).Where(ShouldWarnOnRuntimeIdentifer));
                    if (warnOnRids.Any())
                    {
                        affectedLibs.Add(lib.Name);
                        affectedRids.AddRange(warnOnRids);
                    }
                }

                if (affectedRids.Count > 0)
                {
                    affectedLibs.Sort();
                    affectedRids.Sort();
                    Log.LogWarning(Strings.NonPortableRuntimeIdentifierDetected, string.Join(", ", affectedRids.Distinct()), string.Join(", ", affectedLibs.Distinct()));
                }
            }
        }

        private bool ShouldWarnOnRuntimeIdentifer(string runtimeIdentifier)
        {
            if (string.IsNullOrEmpty(runtimeIdentifier))
                return false;

            int separator = runtimeIdentifier.LastIndexOf('-');
            string platform = separator < 0
                ? runtimeIdentifier
                : runtimeIdentifier.Substring(0, separator);

            return Array.IndexOf(ValidRuntimeIdentifierPlatformsForAssets, platform.ToLowerInvariant()) == -1;
        }

        protected override void ExecuteCore()
        {
            WriteDepsFile(DepsFilePath);
        }
    }
}
