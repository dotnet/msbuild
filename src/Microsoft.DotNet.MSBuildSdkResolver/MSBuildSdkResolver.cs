// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Concurrent;
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
        private readonly VSSettings _vsSettings;

        private static readonly ConcurrentDictionary<string, Version> s_minimumMSBuildVersions
            = new ConcurrentDictionary<string, Version>();

        private static readonly ConcurrentDictionary<CompatibleSdkKey, CompatibleSdkValue> s_compatibleSdks
            = new ConcurrentDictionary<CompatibleSdkKey, CompatibleSdkValue>();

        public DotNetMSBuildSdkResolver() 
            : this(Environment.GetEnvironmentVariable, VSSettings.Ambient)
        {
        }

        // Test constructor
        internal DotNetMSBuildSdkResolver(Func<string, string> getEnvironmentVariable, VSSettings vsSettings)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
            _vsSettings = vsSettings;
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
                string dotnetExeDir = GetDotnetExeDirectory();
                var resolverResult = ResolveNETCoreSdkDirectory(context, dotnetExeDir);
                string netcoreSdkDir = resolverResult.ResolvedSdkDirectory;
                string globalJsonPath = resolverResult.GlobalJsonPath;

                if (netcoreSdkDir == null)
                {
                    return Failure(
                        factory,
                        Strings.UnableToLocateNETCoreSdk);
                }

                msbuildSdksDir = Path.Combine(netcoreSdkDir, "Sdks");
                netcoreSdkVersion = new DirectoryInfo(netcoreSdkDir).Name;

                if (IsNetCoreSDKSmallerThanTheMinimumVersion(netcoreSdkVersion, sdkReference.MinimumVersion))
                {
                    return Failure(
                        factory,
                        Strings.NETCoreSDKSmallerThanMinimumRequestedVersion,
                        netcoreSdkVersion,
                        sdkReference.MinimumVersion);
                }

                Version minimumMSBuildVersion = GetMinimumMSBuildVersion(netcoreSdkDir);
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

            return factory.IndicateSuccess(msbuildSdkDir, netcoreSdkVersion);
        }

        private static SdkResult Failure(SdkResultFactory factory, string format, params object[] args)
        {
            return factory.IndicateFailure(new[] { string.Format(format, args) });
        }

        private sealed class CompatibleSdkKey : IEquatable<CompatibleSdkKey>
        {
            public readonly string DotnetExeDirectory;
            public readonly Version MSBuildVersion;

            public CompatibleSdkKey(string dotnetExeDirectory, Version msbuildVersion)
            {
                DotnetExeDirectory = dotnetExeDirectory;
                MSBuildVersion = msbuildVersion;
            }

            public bool Equals(CompatibleSdkKey other)
            {
                return other != null
                    && DotnetExeDirectory == other.DotnetExeDirectory
                    && MSBuildVersion == other.MSBuildVersion;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as CompatibleSdkValue);
            }

            public override int GetHashCode()
            {
                int h1 = DotnetExeDirectory.GetHashCode();
                int h2 = MSBuildVersion.GetHashCode();
                return unchecked(((h1 << 5) + h1) ^ h2);
            }
        }

        private sealed class CompatibleSdkValue
        {
            public readonly string MostRecentCompatible;
            public readonly string MostRecentCompatibleNonPreview;

            public CompatibleSdkValue(string mostRecentCompatible, string mostRecentCompatibleNonPreview)
            {
                MostRecentCompatible = mostRecentCompatible;
                MostRecentCompatibleNonPreview = mostRecentCompatibleNonPreview;
            }
        }

        private string GetMostCompatibleSdk(string dotnetExeDirectory, Version msbuildVersion)
        {
            CompatibleSdkValue sdks = GetMostCompatibleSdks(dotnetExeDirectory, msbuildVersion);
            if (_vsSettings.DisallowPrerelease())
            {
                return sdks.MostRecentCompatibleNonPreview;
            }

            return sdks.MostRecentCompatible;
        }

        private CompatibleSdkValue GetMostCompatibleSdks(string dotnetExeDirectory, Version msbuildVersion)
        {
            return s_compatibleSdks.GetOrAdd(
                new CompatibleSdkKey(dotnetExeDirectory, msbuildVersion),
                key =>
                {
                    string mostRecent = null;
                    string mostRecentNonPreview = null;

                    string[] availableSdks = NETCoreSdkResolver.GetAvailableSdks(key.DotnetExeDirectory);
                    for (int i = availableSdks.Length - 1; i >= 0; i--)
                    {
                        string netcoreSdkDir = availableSdks[i];
                        string netcoreSdkVersion = new DirectoryInfo(netcoreSdkDir).Name;
                        Version minimumMSBuildVersion = GetMinimumMSBuildVersion(netcoreSdkDir);

                        if (key.MSBuildVersion < minimumMSBuildVersion)
                        {
                            continue;
                        }

                        if (mostRecent == null)
                        {
                            mostRecent = netcoreSdkDir;
                        }

                        if (netcoreSdkVersion.IndexOf('-') < 0)
                        {
                            mostRecentNonPreview = netcoreSdkDir;
                            break;
                        }
                    }

                    return new CompatibleSdkValue(mostRecent, mostRecentNonPreview);
                });
        }

        private Version GetMinimumMSBuildVersion(string netcoreSdkDir)
        {
            return s_minimumMSBuildVersions.GetOrAdd(
                netcoreSdkDir,
                dir => 
                {
                    string minimumVersionFilePath = Path.Combine(netcoreSdkDir, "minimumMSBuildVersion");
                    if (!File.Exists(minimumVersionFilePath))
                    {
                        // smallest version that had resolver support and also
                        // greater than or equal to the version required by any 
                        // .NET Core SDK that did not have this file.
                        return new Version(15, 3, 0);
                    }

                return Version.Parse(File.ReadLines(minimumVersionFilePath).First().Trim());
            });
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

        private NETCoreSdkResolver.Result ResolveNETCoreSdkDirectory(SdkResolverContext context, string dotnetExeDir)
        {
            string globalJsonStartDir = Path.GetDirectoryName(context.SolutionFilePath ?? context.ProjectFilePath);
            var result = NETCoreSdkResolver.ResolveSdk(dotnetExeDir, globalJsonStartDir, _vsSettings.DisallowPrerelease());

            if (result.ResolvedSdkDirectory != null
                && result.GlobalJsonPath == null
                && context.MSBuildVersion < GetMinimumMSBuildVersion(result.ResolvedSdkDirectory))
            {
                string mostCompatible = GetMostCompatibleSdk(dotnetExeDir, context.MSBuildVersion);

                if (mostCompatible != null)
                {
                    result.ResolvedSdkDirectory = mostCompatible;
                }
            }

            return result;
        }

        private string GetDotnetExeDirectory()
        {
            string environmentOverride = _getEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR");
            if (environmentOverride != null)
            {
                return environmentOverride;
            }

            var environmentProvider = new EnvironmentProvider(_getEnvironmentVariable);
            var dotnetExe = environmentProvider.GetCommandPath("dotnet");

            if (dotnetExe != null && !Interop.RunningOnWindows)
            {
                // e.g. on Linux the 'dotnet' command from PATH is a symlink so we need to
                // resolve it to get the actual path to the binary
                dotnetExe = Interop.Unix.realpath(dotnetExe) ?? dotnetExe;
            }

            return Path.GetDirectoryName(dotnetExe);
        }
    }
}
