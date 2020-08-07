// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    // Thread-safety note:
    //  1. MSBuild can call the same resolver instance in parallel on multiple threads.
    //  2. Nevertheless, in the IDE, project re-evaluation can create new instances for each evaluation.
    //
    // As such, all state (instance or static) must be guarded against concurrent access/updates.
    // Caches of minimum versions, compatible SDKs are static to benefit multiple IDE evaluations.
    // VSSettings are also effectively static (singleton instance that can be swapped by tests).

    public sealed class DotNetMSBuildSdkResolver : SdkResolver
    {
        public override string Name => "Microsoft.DotNet.MSBuildSdkResolver";

        // Default resolver has priority 10000 and we want to go before it and leave room on either side of us. 
        public override int Priority => 5000;

        private readonly Func<string, string> _getEnvironmentVariable;
        private readonly NETCoreSdkResolver _netCoreSdkResolver;

        public DotNetMSBuildSdkResolver() 
            : this(Environment.GetEnvironmentVariable, VSSettings.Ambient)
        {
        }

        // Test constructor
        internal DotNetMSBuildSdkResolver(Func<string, string> getEnvironmentVariable, VSSettings vsSettings)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
            _netCoreSdkResolver = new NETCoreSdkResolver(getEnvironmentVariable, vsSettings);
        }

        private sealed class CachedResult
        {
            public string MSBuildSdksDir;
            public string NETCoreSdkVersion;
        }

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext context, SdkResultFactory factory)
        {
            string msbuildSdksDir = null;
            string netcoreSdkVersion = null;
            IDictionary<string, string> propertiesToAdd = null;
            IDictionary<string, SdkResultItem> itemsToAdd = null;
            List<string> warnings = null;

            if (context.State is CachedResult priorResult)
            {
                msbuildSdksDir = priorResult.MSBuildSdksDir;
                netcoreSdkVersion = priorResult.NETCoreSdkVersion;
            }

            if (msbuildSdksDir == null)
            {
                // These are overrides that are used to force the resolved SDK tasks and targets to come from a given
                // base directory and report a given version to msbuild (which may be null if unknown. One key use case
                // for this is to test SDK tasks and targets without deploying them inside the .NET Core SDK.
                msbuildSdksDir = _getEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR");
                netcoreSdkVersion = _getEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER");
            }

            if (msbuildSdksDir == null)
            {
                string dotnetExeDir = _netCoreSdkResolver.GetDotnetExeDirectory();
                string globalJsonStartDir = Path.GetDirectoryName(context.SolutionFilePath ?? context.ProjectFilePath);
                var resolverResult = _netCoreSdkResolver.ResolveNETCoreSdkDirectory(globalJsonStartDir, context.MSBuildVersion, context.IsRunningInVisualStudio, dotnetExeDir);

                if (resolverResult.ResolvedSdkDirectory == null)
                {
                    return Failure(
                        factory,
                        Strings.UnableToLocateNETCoreSdk);
                }

                msbuildSdksDir = Path.Combine(resolverResult.ResolvedSdkDirectory, "Sdks");
                netcoreSdkVersion = new DirectoryInfo(resolverResult.ResolvedSdkDirectory).Name;

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
                    warnings.Add(Strings.GlobalJsonResolutionFailed);
                    if (propertiesToAdd == null)
                    {
                        propertiesToAdd = new Dictionary<string, string>();
                    }
                    propertiesToAdd.Add("SdkResolverHonoredGlobalJson", "false");
                    propertiesToAdd.Add("SdkResolverGlobalJsonPath", resolverResult.GlobalJsonPath);
                }
            }

            context.State = new CachedResult
            {
                MSBuildSdksDir = msbuildSdksDir,
                NETCoreSdkVersion = netcoreSdkVersion
            };

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
