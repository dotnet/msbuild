// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.DotNet.Watcher
{
    public record DotNetWatchOptions(
        bool SuppressHandlingStaticContentFiles,
        bool SuppressMSBuildIncrementalism,
        bool SuppressLaunchBrowser,
        bool SuppressBrowserRefresh,
        bool SuppressEmojis,
        bool RunningAsTest)
    {
        public static DotNetWatchOptions Default { get; } = new DotNetWatchOptions
        (
            SuppressHandlingStaticContentFiles: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_STATIC_FILE_HANDLING"),
            SuppressMSBuildIncrementalism: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_MSBUILD_INCREMENTALISM"),
            SuppressLaunchBrowser: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER"),
            SuppressBrowserRefresh: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"),
            SuppressEmojis: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_EMOJIS"),
            RunningAsTest: IsEnvironmentSet("__DOTNET_WATCH_RUNNING_AS_TEST")
        );

        private static bool IsEnvironmentSet(string key)
        {
            var envValue = Environment.GetEnvironmentVariable(key);
            return envValue == "1" || envValue == "true";
        }
    }
}
