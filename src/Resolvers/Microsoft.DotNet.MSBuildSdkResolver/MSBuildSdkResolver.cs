// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.DotNetSdkResolver;
using Microsoft.DotNet.NativeWrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver;

#nullable disable

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    // Thread-safety note:
    //  1. MSBuild can call the same resolver instance in parallel on multiple threads.
    //  2. Nevertheless, in the IDE, project re-evaluation can create new instances for each evaluation.
    //
    // As such, all state (instance or static) must be guarded against concurrent access/updates.
    // VSSettings are also effectively static (singleton instance that can be swapped by tests).

    public sealed class DotNetMSBuildSdkResolver : SdkResolver
    {
        public override string Name => "Microsoft.DotNet.MSBuildSdkResolver";

        // Default resolver has priority 10000 and we want to go before it and leave room on either side of us. 
        public override int Priority => 5000;

        private readonly Func<string, string> _getEnvironmentVariable;
        private readonly NETCoreSdkResolver _netCoreSdkResolver;

        private static CachingWorkloadResolver _staticWorkloadResolver = new CachingWorkloadResolver();

        public DotNetMSBuildSdkResolver() 
            : this(Environment.GetEnvironmentVariable, VSSettings.Ambient)
        {
        }

        // Test constructor
        public DotNetMSBuildSdkResolver(Func<string, string> getEnvironmentVariable, VSSettings vsSettings)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
            _netCoreSdkResolver = new NETCoreSdkResolver(getEnvironmentVariable, vsSettings);
        }

        private sealed class CachedState
        {
            public string DotnetRoot;
            public string MSBuildSdksDir;
            public string NETCoreSdkVersion;
            public IDictionary<string, string> PropertiesToAdd;
            public CachingWorkloadResolver WorkloadResolver;
        }

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext context, SdkResultFactory factory)
        {
            string dotnetRoot = null;
            string msbuildSdksDir = null;
            string netcoreSdkVersion = null;
            IDictionary<string, string> propertiesToAdd = null;
            IDictionary<string, SdkResultItem> itemsToAdd = null;
            List<string> warnings = null;
            CachingWorkloadResolver workloadResolver = null;

            if (context.State is CachedState priorResult)
            {
                dotnetRoot = priorResult.DotnetRoot;
                msbuildSdksDir = priorResult.MSBuildSdksDir;
                netcoreSdkVersion = priorResult.NETCoreSdkVersion;
                propertiesToAdd = priorResult.PropertiesToAdd;
                workloadResolver = priorResult.WorkloadResolver;
            }

            if (context.IsRunningInVisualStudio)
            {
                workloadResolver = _staticWorkloadResolver;
            }

            if (workloadResolver == null)
            {
                workloadResolver = new CachingWorkloadResolver();
            }

            if (msbuildSdksDir == null)
            {
                dotnetRoot = EnvironmentProvider.GetDotnetExeDirectory(_getEnvironmentVariable);
                string globalJsonStartDir = GetGlobalJsonStartDir(context);
                var resolverResult = _netCoreSdkResolver.ResolveNETCoreSdkDirectory(globalJsonStartDir, context.MSBuildVersion, context.IsRunningInVisualStudio, dotnetRoot);

                if (resolverResult.ResolvedSdkDirectory == null)
                {
                    return Failure(
                        factory,
                        Strings.UnableToLocateNETCoreSdk);
                }

                msbuildSdksDir = Path.Combine(resolverResult.ResolvedSdkDirectory, "Sdks");
                netcoreSdkVersion = new DirectoryInfo(resolverResult.ResolvedSdkDirectory).Name;

                // These are overrides that are used to force the resolved SDK tasks and targets to come from a given
                // base directory and report a given version to msbuild (which may be null if unknown. One key use case
                // for this is to test SDK tasks and targets without deploying them inside the .NET Core SDK.
                var msbuildSdksDirFromEnv = _getEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR");
                var netcoreSdkVersionFromEnv = _getEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER");
                if (!string.IsNullOrEmpty(msbuildSdksDirFromEnv))
                {
                    msbuildSdksDir = msbuildSdksDirFromEnv;
                }
                if (!string.IsNullOrEmpty(netcoreSdkVersionFromEnv))
                {
                    netcoreSdkVersion = netcoreSdkVersionFromEnv;
                }

                if (IsNetCoreSDKSmallerThanTheMinimumVersion(netcoreSdkVersion, sdkReference.MinimumVersion))
                {
                    return Failure(
                        factory,
                        Strings.NETCoreSDKSmallerThanMinimumRequestedVersion,
                        netcoreSdkVersion,
                        sdkReference.MinimumVersion);
                }

                Version minimumMSBuildVersion = _netCoreSdkResolver.GetMinimumMSBuildVersion(resolverResult.ResolvedSdkDirectory);
                if (context.MSBuildVersion < minimumMSBuildVersion)
                {
                    return Failure(
                        factory,
                        Strings.MSBuildSmallerThanMinimumVersion,
                        netcoreSdkVersion,
                        minimumMSBuildVersion,
                        context.MSBuildVersion);
                }

                string minimumVSDefinedSDKVersion = GetMinimumVSDefinedSDKVersion();
                if (IsNetCoreSDKSmallerThanTheMinimumVersion(netcoreSdkVersion, minimumVSDefinedSDKVersion))
                {
                    return Failure(
                        factory,
                        Strings.NETCoreSDKSmallerThanMinimumVersionRequiredByVisualStudio,
                        netcoreSdkVersion,
                        minimumVSDefinedSDKVersion);
                }

                if (resolverResult.FailedToResolveSDKSpecifiedInGlobalJson)
                {
                    if (warnings == null)
                    {
                        warnings = new List<string>();
                    }

                    if (!string.IsNullOrWhiteSpace(resolverResult.RequestedVersion))
                    {
                        warnings.Add(string.Format(Strings.GlobalJsonResolutionFailedSpecificVersion, resolverResult.RequestedVersion));
                    }
                    else
                    {
                        warnings.Add(Strings.GlobalJsonResolutionFailed);
                    }

                    if (propertiesToAdd == null)
                    {
                        propertiesToAdd = new Dictionary<string, string>();
                    }
                    propertiesToAdd.Add("SdkResolverHonoredGlobalJson", "false");
                    propertiesToAdd.Add("SdkResolverGlobalJsonPath", resolverResult.GlobalJsonPath);
                }
            }

            context.State = new CachedState
            {
                DotnetRoot = dotnetRoot,
                MSBuildSdksDir = msbuildSdksDir,
                NETCoreSdkVersion = netcoreSdkVersion,
                PropertiesToAdd = propertiesToAdd,
                WorkloadResolver = workloadResolver
            };

            //  First check if requested SDK resolves to a workload SDK pack
            string userProfileDir = CliFolderPathCalculatorCore.GetDotnetUserProfileFolderPath();
            var workloadResult = workloadResolver.Resolve(sdkReference.Name, dotnetRoot, netcoreSdkVersion, userProfileDir);

            if (workloadResult is not CachingWorkloadResolver.NullResolutionResult)
            {
                return workloadResult.ToSdkResult(sdkReference, factory);
            }

            string msbuildSdkDir = Path.Combine(msbuildSdksDir, sdkReference.Name, "Sdk");
            if (!Directory.Exists(msbuildSdkDir))
            {
                return Failure(
                    factory,
                    Strings.MSBuildSDKDirectoryNotFound,
                    msbuildSdkDir);
            }

            return factory.IndicateSuccess(msbuildSdkDir, netcoreSdkVersion, propertiesToAdd, itemsToAdd, warnings);
        }

        private static SdkResult Failure(SdkResultFactory factory, string format, params object[] args)
        {
            return factory.IndicateFailure(new[] { string.Format(format, args) });
        }

        /// <summary>
        /// Gets the starting path to search for global.json.
        /// </summary>
        /// <param name="context">A <see cref="SdkResolverContext" /> that specifies where the current project is located.</param>
        /// <returns>The full path to a starting directory to use when searching for a global.json.</returns>
        private static string GetGlobalJsonStartDir(SdkResolverContext context)
        {
            // Evaluating in-memory projects with MSBuild means that they won't have a solution or project path.
            // Default to using the current directory as a best effort to finding a global.json.  This could result in
            // using the wrong one but without a starting directory, SDK resolution won't work at all.  In most cases, a
            // global.json won't be found and the default SDK will be used.

            string startDir = Environment.CurrentDirectory;

            if (!string.IsNullOrWhiteSpace(context.SolutionFilePath))
            {
                startDir = Path.GetDirectoryName(context.SolutionFilePath);
            }
            else if(!string.IsNullOrWhiteSpace(context.ProjectFilePath))
            {
                startDir = Path.GetDirectoryName(context.ProjectFilePath);
            }

            return startDir;
        }

        private static string GetMinimumVSDefinedSDKVersion()
        {
            string dotnetMSBuildSdkResolverDirectory =
                Path.GetDirectoryName(typeof(DotNetMSBuildSdkResolver).GetTypeInfo().Assembly.Location);
            string minimumVSDefinedSdkVersionFilePath =
                Path.Combine(dotnetMSBuildSdkResolverDirectory, "minimumVSDefinedSDKVersion");

            if (!File.Exists(minimumVSDefinedSdkVersionFilePath))
            {
                // smallest version that is required by VS 15.3.
                return "1.0.4";
            }

            return File.ReadLines(minimumVSDefinedSdkVersionFilePath).First().Trim();
        }

        private bool IsNetCoreSDKSmallerThanTheMinimumVersion(string netcoreSdkVersion, string minimumVersion)
        {
            FXVersion netCoreSdkFXVersion;
            FXVersion minimumFXVersion;

            if (string.IsNullOrEmpty(minimumVersion))
            {
                return false;
            }

            if (!FXVersion.TryParse(netcoreSdkVersion, out netCoreSdkFXVersion) ||
                !FXVersion.TryParse(minimumVersion, out minimumFXVersion))
            {
                return true;
            }

            return FXVersion.Compare(netCoreSdkFXVersion, minimumFXVersion) < 0;
        }


    }
}
