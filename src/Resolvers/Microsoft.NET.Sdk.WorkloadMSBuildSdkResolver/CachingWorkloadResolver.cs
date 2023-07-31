// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Collections.Immutable;

#if NET
using Microsoft.DotNet.Cli;
#else
using Microsoft.DotNet.DotNetSdkResolver;
#endif

#nullable disable

namespace Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver
{

    //  This class contains workload SDK resolution logic which will be used by both .NET SDK MSBuild and Full Framework / Visual Studio MSBuild.
    //
    //  Keeping this performant in Visual Studio is tricky, as VS performs a lot of evaluations, but they are not linked by an MSBuild "Submission ID",
    //  so the state caching support provided by MSBuild for SDK Resolvers doesn't really help.  Additionally, multiple instances of the SDK resolver
    //  may be created, and the same instance may be called on multiple threads.  So state needs to be cached staticly and be thread-safe.
    //
    //  To keep the state static, the MSBuildSdkResolver keeps a static reference to the CachingWorkloadResolver that is used if the build is inside
    //  Visual Studio.  To keep it thread-safe, the body of the Resolve method is all protected by a lock statement.  This avoids having to make
    //  the classes consumed by the CachingWorkloadResolver (the manifest provider and workload resolver) thread-safe.
    //
    //  A resolver should not over-cache and return out-of-date results.  For workloads, the resolution could change due to:
    //  - Installation, update, or uninstallation of a workload
    //  - Resolved SDK changes (either due to an SDK installation or uninstallation, or a global.json change)
    //  For SDK or workload installation actions, we expect to be running under a new process since Visual Studio will have been restarted.
    //  For global.json changes, the Resolve method takes parameters for the dotnet root and the SDK version.  If those values have changed
    //  from the previous call, the cached state will be thrown out and recreated.
    //  We don't currently handle the case where a global.json file is edited to change the workload version.  It may be necessary
    //  to kill running MSBuild processes to get that change to take effect.
    class CachingWorkloadResolver
    {
        private sealed record CachedState
        {            
            public string DotnetRootPath { get; init; }
            public string SdkVersion { get; init; }
            public string GlobalJsonPath { get; init; }
            public IWorkloadManifestProvider ManifestProvider { get; init; }
            public IWorkloadResolver WorkloadResolver { get; init; }
            public ImmutableDictionary<string, ResolutionResult> CachedResults { get; init; }

            public CachedState()
            {
                CachedResults = ImmutableDictionary.Create<string, ResolutionResult>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public object _lockObject { get; } = new object();
        private CachedState _cachedState;
        private readonly bool _enabled;


        public CachingWorkloadResolver()
        {
            // Support opt-out for workload resolution
            _enabled = true;
            var envVar = Environment.GetEnvironmentVariable("MSBuildEnableWorkloadResolver");
            if (envVar != null)
            {
                if (envVar.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    _enabled = false;
                }
            }

            if (_enabled)
            {
                string sentinelPath = Path.Combine(Path.GetDirectoryName(typeof(CachingWorkloadResolver).Assembly.Location), "DisableWorkloadResolver.sentinel");
                if (File.Exists(sentinelPath))
                {
                    _enabled = false;
                }
            }
        }

        public record ResolutionResult()
        {
            public SdkResult ToSdkResult(SdkReference sdkReference, SdkResultFactory factory)
            {
                switch (this)
                {
                    case SinglePathResolutionResult r:
                        return factory.IndicateSuccess(r.Path, sdkReference.Version);
                    case MultiplePathResolutionResult r:
                        return factory.IndicateSuccess(r.Paths, sdkReference.Version);
                    case EmptyResolutionResult r:
                        return factory.IndicateSuccess(Enumerable.Empty<string>(), sdkReference.Version, r.propertiesToAdd, r.itemsToAdd);
                    case NullResolutionResult:
                        return null;
                }

                throw new InvalidOperationException("Unknown resolutionResult type: " + this.GetType());
            }
        }

        public sealed record SinglePathResolutionResult(
            string Path
        ) : ResolutionResult;

        public sealed record MultiplePathResolutionResult(
            IEnumerable<string> Paths
        ) : ResolutionResult;

        public sealed record EmptyResolutionResult(
            IDictionary<string, string> propertiesToAdd,
            IDictionary<string, SdkResultItem> itemsToAdd
        ) : ResolutionResult;

        public sealed record NullResolutionResult() : ResolutionResult;

        private static ResolutionResult Resolve(string sdkReferenceName, IWorkloadManifestProvider manifestProvider, IWorkloadResolver workloadResolver)
        {
            if (sdkReferenceName.Equals("Microsoft.NET.SDK.WorkloadAutoImportPropsLocator", StringComparison.OrdinalIgnoreCase))
            {
                List<string> autoImportSdkPaths = new List<string>();
                foreach (var sdkPackInfo in workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk))
                {
                    string sdkPackSdkFolder = Path.Combine(sdkPackInfo.Path, "Sdk");
                    string autoImportPath = Path.Combine(sdkPackSdkFolder, "AutoImport.props");
                    if (File.Exists(autoImportPath))
                    {
                        autoImportSdkPaths.Add(sdkPackSdkFolder);
                    }
                }
                //  Call Distinct() here because with aliased packs, there may be duplicates of the same path
                return new MultiplePathResolutionResult(autoImportSdkPaths.Distinct());
            }
            else if (sdkReferenceName.Equals("Microsoft.NET.SDK.WorkloadManifestTargetsLocator", StringComparison.OrdinalIgnoreCase))
            {
                List<string> workloadManifestPaths = new List<string>();
                foreach (var manifestDirectory in manifestProvider.GetManifests().Select(m => m.ManifestDirectory))
                {
                    var workloadManifestTargetPath = Path.Combine(manifestDirectory, "WorkloadManifest.targets");
                    if (File.Exists(workloadManifestTargetPath))
                    {
                        workloadManifestPaths.Add(manifestDirectory);
                    }
                }
                return new MultiplePathResolutionResult(workloadManifestPaths);
            }
            else
            {
                var packInfo = workloadResolver.TryGetPackInfo(new WorkloadPackId (sdkReferenceName));
                if (packInfo != null)
                {
                    if (Directory.Exists(packInfo.Path))
                    {
                        return new SinglePathResolutionResult(Path.Combine(packInfo.Path, "Sdk"));
                    }
                    else
                    {
                        var itemsToAdd = new Dictionary<string, SdkResultItem>();
                        itemsToAdd.Add("MissingWorkloadPack",
                            new SdkResultItem(sdkReferenceName,
                                metadata: new Dictionary<string, string>()
                                {
                                    { "Version", packInfo.Version }
                                }));

                        Dictionary<string, string> propertiesToAdd = new Dictionary<string, string>();
                        return new EmptyResolutionResult(propertiesToAdd, itemsToAdd);
                    }
                }
            }
            return new NullResolutionResult();
        }

        public ResolutionResult Resolve(string sdkReferenceName, string dotnetRootPath, string sdkVersion, string userProfileDir, string globalJsonPath)
        {
            if (!_enabled)
            {
                return new NullResolutionResult();
            }

            ResolutionResult resolutionResult;

            lock (_lockObject)
            {
                if (_cachedState == null ||
                    _cachedState.DotnetRootPath != dotnetRootPath ||
                    _cachedState.SdkVersion != sdkVersion ||
                    _cachedState.GlobalJsonPath != globalJsonPath)
                {
                    var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetRootPath, sdkVersion, userProfileDir, globalJsonPath);
                    var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetRootPath, sdkVersion, userProfileDir);

                    _cachedState = new CachedState()
                    {
                        DotnetRootPath = dotnetRootPath,
                        SdkVersion = sdkVersion,
                        GlobalJsonPath = globalJsonPath,
                        ManifestProvider = workloadManifestProvider,
                        WorkloadResolver = workloadResolver
                    };
                }

                if (!_cachedState.CachedResults.TryGetValue(sdkReferenceName, out resolutionResult))
                {
                    resolutionResult = Resolve(sdkReferenceName, _cachedState.ManifestProvider, _cachedState.WorkloadResolver);

                    _cachedState = _cachedState with
                    {
                        CachedResults = _cachedState.CachedResults.Add(sdkReferenceName, resolutionResult)
                    };
                }
            }

            return resolutionResult;
        }
    }
}
