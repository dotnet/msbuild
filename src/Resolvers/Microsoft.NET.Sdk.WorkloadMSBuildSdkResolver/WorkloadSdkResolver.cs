using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;

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

        private IWorkloadManifestProvider _workloadManifestProvider;
        private IWorkloadResolver _workloadResolver;


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

        private void InitializeWorkloadResolver(SdkResolverContext context)
        {
            var dotnetRootPath = GetDotNetRoot(context);

            var sdkDirectory = GetSdkDirectory(context);
            //  The SDK version is the name of the SDK directory (ie dotnet\sdk\5.0.100)
            var sdkVersion = Path.GetFileName(sdkDirectory);

            _workloadManifestProvider ??= new SdkDirectoryWorkloadManifestProvider(dotnetRootPath, sdkVersion);
            
            _workloadResolver ??= new WorkloadResolver(_workloadManifestProvider, dotnetRootPath);
        }

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            if (!_enabled)
            {
                return null;
            }

            InitializeWorkloadResolver(resolverContext);

            if (sdkReference.Name.Equals("Microsoft.NET.SDK.WorkloadAutoImportPropsLocator", StringComparison.OrdinalIgnoreCase))
            {
                List<string> autoImportSdkPaths = new List<string>();
                foreach (var sdkPackInfo in _workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk))
                {
                    string sdkPackSdkFolder = Path.Combine(sdkPackInfo.Path, "Sdk");
                    string autoImportPath = Path.Combine(sdkPackSdkFolder, "AutoImport.props");
                    if (File.Exists(autoImportPath))
                    {
                        autoImportSdkPaths.Add(sdkPackSdkFolder);
                    }
                }
                return factory.IndicateSuccess(autoImportSdkPaths, sdkReference.Version);
            }
            else if (sdkReference.Name.Equals("Microsoft.NET.SDK.WorkloadManifestTargetsLocator", StringComparison.OrdinalIgnoreCase))
            {
                List<string> workloadManifestPaths = new List<string>();
                foreach (var manifestDirectory in _workloadManifestProvider.GetManifestDirectories())
                {
                    var workloadManifestTargetPath = Path.Combine(manifestDirectory, "WorkloadManifest.targets");
                    if (File.Exists(workloadManifestTargetPath))
                    {
                        workloadManifestPaths.Add(manifestDirectory);
                    }
                }
                return factory.IndicateSuccess(workloadManifestPaths, sdkReference.Version);
            }
            else
            {
                var packInfo = _workloadResolver.TryGetPackInfo(sdkReference.Name);
                if (packInfo != null)
                {
                    if (Directory.Exists(packInfo.Path))
                    {
                        return factory.IndicateSuccess(Path.Combine(packInfo.Path, "Sdk"), sdkReference.Version);
                    }
                    else
                    {
                        var itemsToAdd = new Dictionary<string, SdkResultItem>();
                        itemsToAdd.Add("MissingWorkloadPack",
                            new SdkResultItem(sdkReference.Name,
                                metadata: new Dictionary<string, string>()
                                {
                                    { "Version", packInfo.Version }
                                }));

                        Dictionary<string, string> propertiesToAdd = new Dictionary<string, string>();
                        return factory.IndicateSuccess(Enumerable.Empty<string>(),
                            sdkReference.Version,
                            propertiesToAdd: propertiesToAdd,
                            itemsToAdd: itemsToAdd);
                    }
                }
            }
            return null;
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
