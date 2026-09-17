// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable enable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Helpers for locating the versioned .NET SDK directory (the <c>sdk/&lt;version&gt;/</c> folder that
    /// contains MSBuild) when MSBuild is hosted by the .NET SDK CLI.
    /// </summary>
    /// <remarks>
    /// When the SDK hosts MSBuild in a trimmed or Native AOT process the muxer loads the SDK entry point
    /// directly, so the BCL "where am I" APIs do not point at the versioned SDK directory:
    /// <see cref="AppContext.BaseDirectory"/> and <c>Environment.ProcessPath</c> resolve to the install
    /// root (the muxer's own directory) and <c>Assembly.Location</c> is empty. The SDK therefore publishes
    /// the resolved SDK directory as the <see cref="SdkRootAppContextName"/> AppContext value so hosted
    /// components can resolve SDK-relative paths from it rather than probing a dll path. This mirrors
    /// <c>Microsoft.DotNet.Cli.Utils.SdkPaths</c> on the SDK side; the data name must stay in sync with it.
    /// </remarks>
    internal static class DotNetSdkPaths
    {
        /// <summary>
        /// The <see cref="AppContext"/> data name the .NET SDK uses to publish the resolved versioned SDK
        /// directory for the components it hosts. An AppContext value is process-local (unlike an
        /// environment variable it is not inherited by child processes) and can also be supplied through a
        /// <c>runtimeconfig.json</c> <c>configProperties</c> entry. Must match
        /// <c>Microsoft.DotNet.Cli.Utils.SdkPaths.DataName</c>.
        /// </summary>
        internal const string SdkRootAppContextName = "Microsoft.DotNet.Sdk.Root";

        /// <summary>
        /// The versioned SDK directory the .NET SDK host published through the
        /// <see cref="SdkRootAppContextName"/> AppContext value, or <see langword="null"/> when no host
        /// published one (for example a normal JIT MSBuild that discovers itself from its own path).
        /// </summary>
        internal static string? SdkRootFromAppContext => AppContext.GetData(SdkRootAppContextName) as string;
    }
}
