using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public class WorkloadSdkResolver : SdkResolver
    {
        public override string Name => "Microsoft.DotNet.MSBuildWorkloadSdkResolver";

        public override int Priority => 4000;

        private bool _enabled;


        //  MSBuild SDK resolvers can use the SdkResolverContext's State property to cache state across multiple calls.  However,
        //  MSBuild only caches those within the same build (which is tracked via a build submission ID).
        //  When running in Visual Studio, there are lots of evaluations that aren't associated with with a build, and then
        //  it looks like each project is evaluated under a separate build submission ID.  So the built-in caching support doesn't
        //  work well when running in Visual Studio.
        //  Because of this, when running in Visual Studio, we will use our own cache state, which will be stored staticly.  In order
        //  to avoid requiring that the workload manifest reader and resolver classes are fully thread safe, we include a lock object
        //  in the cache state which we use to ensure that multiple threads don't access it at the same time.  We don't expect high
        //  concurrency in MSBuild SDK Resolver calls, so we don't expect the lock to impact the performance.
        private static CachedState _staticCachedState;

#if NETFRAMEWORK
        private readonly NETCoreSdkResolver _sdkResolver;
#endif

        public WorkloadSdkResolver()
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

            if (!_enabled)
            {
                string sentinelPath = Path.Combine(Path.GetDirectoryName(typeof(WorkloadSdkResolver).Assembly.Location), "DisableWorkloadResolver.sentinel");
                if (File.Exists(sentinelPath))
                {
                    _enabled = false;
                }
            }

#if NETFRAMEWORK
            if (_enabled)
            {
                _sdkResolver = new NETCoreSdkResolver();
            }
#endif
        }

        private record CachedState
        {
            public object LockObject { get; init; }
            public string DotnetRootPath { get; init; }
            public string SdkVersion { get; init; }
            public IWorkloadManifestProvider ManifestProvider { get; init; }
            public IWorkloadResolver WorkloadResolver { get; init; }
            public ImmutableDictionary<string, ResolutionResult> CachedResults { get; init; }

            public CachedState()
            {
                LockObject = new object();
                CachedResults = ImmutableDictionary.Create<string, ResolutionResult>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private record ResolutionResult();

        private record SinglePathResolutionResult(
            string Path
        ) : ResolutionResult;

        private record MultiplePathResolutionResult(
            IEnumerable<string> Paths
        ) : ResolutionResult;

        private record EmptyResolutionResult(
            IDictionary<string, string> propertiesToAdd,
            IDictionary<string, SdkResultItem> itemsToAdd
        ) : ResolutionResult;

        private record NullResolutionResult() : ResolutionResult;
            
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
                foreach (var manifestDirectory in manifestProvider.GetManifestDirectories())
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
                var packInfo = workloadResolver.TryGetPackInfo(sdkReferenceName);
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

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            if (!_enabled)
            {
                return null;
            }

            CachedState cachedState = null;

            if (resolverContext.IsRunningInVisualStudio)
            {
                cachedState = _staticCachedState;
            }
            else
            {
                if (resolverContext.State is CachedState resolverContextState)
                {
                    cachedState = resolverContextState;
                }
            }

            
            if (cachedState == null)
            {
                //  If we don't have any cached state yet, then find the dotnet directory and SDK version, and create the workload resolver classes
                //  Note that in Visual Studio, we could end up doing this multiple times if the resolver gets called on multiple threads before any
                //  state has been cached.

                var dotnetRootPath = GetDotNetRoot(resolverContext);

                var sdkDirectory = GetSdkDirectory(resolverContext);
                //  The SDK version is the name of the SDK directory (ie dotnet\sdk\5.0.100)
                var sdkVersion = Path.GetFileName(sdkDirectory);

                var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetRootPath, sdkVersion);
                var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetRootPath, sdkVersion);

                cachedState = new CachedState()
                {
                    DotnetRootPath = dotnetRootPath,
                    SdkVersion = sdkVersion,
                    ManifestProvider = workloadManifestProvider,
                    WorkloadResolver = workloadResolver
                };
            }

            ResolutionResult resolutionResult;
            lock (cachedState.LockObject)
            {
                if (!cachedState.CachedResults.TryGetValue(sdkReference.Name, out resolutionResult))
                {
                    resolutionResult = Resolve(sdkReference.Name, cachedState.ManifestProvider, cachedState.WorkloadResolver);

                    cachedState = cachedState with
                    {
                        CachedResults = cachedState.CachedResults.Add(sdkReference.Name, resolutionResult)
                    };

                    resolverContext.State = cachedState;

                    if (resolverContext.IsRunningInVisualStudio)
                    {
                        _staticCachedState = cachedState;
                    }
                }
            }

            switch (resolutionResult)
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

            throw new InvalidOperationException("Unknown resolutionResult type: " + resolutionResult.GetType());

        }

        private string GetSdkDirectory(SdkResolverContext context)
        {
#if NET
            var sdkDirectory = Path.GetDirectoryName(typeof(DotnetFiles).Assembly.Location);
            return sdkDirectory;

#else
            string dotnetExeDir = _sdkResolver.GetDotnetExeDirectory();
            string globalJsonStartDir = Path.GetDirectoryName(context.SolutionFilePath ?? context.ProjectFilePath);
            var sdkResolutionResult = _sdkResolver.ResolveNETCoreSdkDirectory(globalJsonStartDir, context.MSBuildVersion, context.IsRunningInVisualStudio, dotnetExeDir);

            return sdkResolutionResult.ResolvedSdkDirectory;
#endif

        }

        private string GetDotNetRoot(SdkResolverContext context)
        {
            var sdkDirectory = GetSdkDirectory(context);
            var dotnetRoot = Directory.GetParent(sdkDirectory).Parent.FullName;
            return dotnetRoot;
        }
    }
}

#if !NET
namespace System.Runtime.CompilerServices
{
  public class IsExternalInit { }
}
#endif
