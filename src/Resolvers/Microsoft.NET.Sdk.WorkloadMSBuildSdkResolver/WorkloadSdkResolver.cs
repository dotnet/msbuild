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

#if NETFRAMEWORK
        private readonly NETCoreSdkResolver _sdkResolver;
#endif

        public WorkloadSdkResolver()
        {
            //  Put workload resolution behind a feature flag.
            _enabled = false;
            var envVar = Environment.GetEnvironmentVariable("MSBuildEnableWorkloadResolver");
            if (envVar != null)
            {
                if (envVar.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    _enabled = true;
                }
            }

            if (!_enabled)
            {
                string sentinelPath = Path.Combine(Path.GetDirectoryName(typeof(WorkloadSdkResolver).Assembly.Location), "EnableWorkloadResolver.sentinel");
                if (File.Exists(sentinelPath))
                {
                    _enabled = true;
                }
            }

#if NETFRAMEWORK
            if (_enabled)
            {
                _sdkResolver = new NETCoreSdkResolver();
            }
#endif
        }

        private record CachedState(
            string DotNetRootPath,
            string SdkVersion,
            ImmutableDictionary<string, ResolutionResult> CachedResults
        );

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

            CachedState cachedState;

            if (resolverContext.State is CachedState resolverContextState)
            {
                cachedState = resolverContextState;
            }
            else
            {
                cachedState = new CachedState(null, null, ImmutableDictionary.Create<string, ResolutionResult>(StringComparer.OrdinalIgnoreCase));
            }

            ResolutionResult resolutionResult;

            if (!cachedState.CachedResults.TryGetValue(sdkReference.Name, out resolutionResult))
            {
                if (cachedState.DotNetRootPath == null || cachedState.SdkVersion == null)
                {
                    var dotnetRootPath = GetDotNetRoot(resolverContext);

                    var sdkDirectory = GetSdkDirectory(resolverContext);
                    //  The SDK version is the name of the SDK directory (ie dotnet\sdk\5.0.100)
                    var sdkVersion = Path.GetFileName(sdkDirectory);

                    cachedState = cachedState with
                    {
                        DotNetRootPath = dotnetRootPath,
                        SdkVersion = sdkVersion
                    };

                }

                var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(cachedState.DotNetRootPath, cachedState.SdkVersion);
                var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, cachedState.DotNetRootPath, cachedState.SdkVersion);

                resolutionResult = Resolve(sdkReference.Name, workloadManifestProvider, workloadResolver);

                cachedState = cachedState with
                {
                    CachedResults = cachedState.CachedResults.Add(sdkReference.Name, resolutionResult)
                };

                resolverContext.State = cachedState;
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
