// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable IDE0240 // Nullable directive is redundant (when file is included to a project that already enables nullable
#nullable enable

using System.Runtime.InteropServices;
using System;

namespace Microsoft.DotNet.Cli
{
    static class EnvironmentVariableNames
    {
        public static readonly string ALLOW_TARGETING_PACK_CACHING = "DOTNETSDK_ALLOW_TARGETING_PACK_CACHING";
        public static readonly string WORKLOAD_PACK_ROOTS = "DOTNETSDK_WORKLOAD_PACK_ROOTS";
        public static readonly string WORKLOAD_MANIFEST_ROOTS = "DOTNETSDK_WORKLOAD_MANIFEST_ROOTS";
        public static readonly string WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS = "DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS";
        public static readonly string WORKLOAD_UPDATE_NOTIFY_DISABLE = "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE";
        public static readonly string WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS = "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS";
        public static readonly string WORKLOAD_DISABLE_PACK_GROUPS = "DOTNET_CLI_WORKLOAD_DISABLE_PACK_GROUPS";
        public static readonly string DOTNET_CLI_FORCE_UTF8_ENCODING = nameof(DOTNET_CLI_FORCE_UTF8_ENCODING);
        public static readonly string TELEMETRY_OPTOUT = "DOTNET_CLI_TELEMETRY_OPTOUT";
        public static readonly string ENABLE_PUBLISH_RELEASE_FOR_SOLUTIONS = "DOTNET_CLI_ENABLE_PUBLISH_RELEASE_FOR_SOLUTIONS";
        public static readonly string ENABLE_PACK_RELEASE_FOR_SOLUTIONS = "DOTNET_CLI_ENABLE_PACK_RELEASE_FOR_SOLUTIONS";
        public static readonly string DOTNET_ROOT = "DOTNET_ROOT";
		
        public static readonly string DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG = "DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG";
        public static readonly string DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR = "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR";
        public static readonly string DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER = "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER";
        public static readonly string DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT = "DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT";

#if NET7_0_OR_GREATER
        private static readonly Version s_version6_0 = new(6, 0);

        public static string? TryGetDotNetRootVariableName(string runtimeIdentifier, string defaultAppHostRuntimeIdentifier, string targetFrameworkVersion)
            => TryGetDotNetRootVariableName(runtimeIdentifier, defaultAppHostRuntimeIdentifier, TryParseTargetFrameworkVersion(targetFrameworkVersion));

        public static string? TryGetDotNetRootVariableName(string runtimeIdentifier, string defaultAppHostRuntimeIdentifier, Version? targetFrameworkVersion)
            => TryGetDotNetRootVariableNameImpl(runtimeIdentifier, defaultAppHostRuntimeIdentifier, targetFrameworkVersion, RuntimeInformation.ProcessArchitecture, Environment.Is64BitProcess);

        internal static string? TryGetDotNetRootVariableNameImpl(string runtimeIdentifier, string defaultAppHostRuntimeIdentifier, Version? targetFrameworkVersion, Architecture currentArchitecture, bool is64bit)
        {
            // If the app targets the same architecture as SDK is running on or an unknown architecture, set DOTNET_ROOT, DOTNET_ROOT(x86) for 32-bit, DOTNET_ROOT_arch for TFM 6+.
            // If the app targets different architecture from the SDK, do not set DOTNET_ROOT.

            if (!TryParseArchitecture(runtimeIdentifier, out var targetArchitecture) && !TryParseArchitecture(defaultAppHostRuntimeIdentifier, out targetArchitecture) ||
                targetArchitecture == currentArchitecture)
            {
                var suffix = targetFrameworkVersion != null && targetFrameworkVersion >= s_version6_0 ?
                    $"_{currentArchitecture.ToString().ToUpperInvariant()}" :
                    is64bit ? "" : "(x86)";

                return DOTNET_ROOT + suffix;
            }

            return null;
        }

        internal static bool TryParseArchitecture(string runtimeIdentifier, out Architecture architecture)
        {
            // RID is [os].[version]-[architecture]-[additional qualifiers]
            // See https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

            int archStart = runtimeIdentifier.IndexOf('-') + 1;
            if (archStart <= 0)
            {
                architecture = default;
                return false;
            }

            int archEnd = runtimeIdentifier.IndexOf('-', archStart);
            var span = runtimeIdentifier.AsSpan(archStart, (archEnd > 0 ? archEnd : runtimeIdentifier.Length) - archStart);

            return Enum.TryParse(span, ignoreCase: true, out architecture);
        }

        public static Version? TryParseTargetFrameworkVersion(string targetFrameworkVersion)
        {
            // TargetFrameworkVersion appears as "vX.Y" in msbuild. Ignore the leading 'v'.
            return !string.IsNullOrEmpty(targetFrameworkVersion) && Version.TryParse(targetFrameworkVersion.Substring(1), out var version) ? version : null;
        }
#endif
    }
}
