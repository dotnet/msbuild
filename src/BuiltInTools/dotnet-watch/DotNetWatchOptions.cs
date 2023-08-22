// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher
{
    [Flags]
    internal enum TestFlags
    {
        None = 0,
        RunningAsTest = 1 << 0,
        BrowserRequired = 1 << 1,
    }

    internal sealed record DotNetWatchOptions(
        bool SuppressHandlingStaticContentFiles,
        bool SuppressMSBuildIncrementalism,
        bool SuppressLaunchBrowser,
        bool SuppressBrowserRefresh,
        bool SuppressEmojis,
        TestFlags TestFlags)
    {
        public static DotNetWatchOptions Default { get; } = new DotNetWatchOptions
        (
            SuppressHandlingStaticContentFiles: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_STATIC_FILE_HANDLING"),
            SuppressMSBuildIncrementalism: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_MSBUILD_INCREMENTALISM"),
            SuppressLaunchBrowser: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER"),
            SuppressBrowserRefresh: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"),
            SuppressEmojis: IsEnvironmentSet("DOTNET_WATCH_SUPPRESS_EMOJIS"),
            TestFlags: Environment.GetEnvironmentVariable("__DOTNET_WATCH_TEST_FLAGS") is { } value ? Enum.Parse<TestFlags>(value) : TestFlags.None
        );

        public bool NonInteractive { get; set; }

        public bool RunningAsTest { get => ((TestFlags & TestFlags.RunningAsTest) != TestFlags.None); }

        private static bool IsEnvironmentSet(string key)
        {
            var envValue = Environment.GetEnvironmentVariable(key);
            return envValue == "1" || string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
