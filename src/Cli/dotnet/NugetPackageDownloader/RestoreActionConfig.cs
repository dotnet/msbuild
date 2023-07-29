// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    /// <param name="Interactive">The flag to enable nuget authentication plugin.
    /// https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-cross-platform-authentication-plugin</param>
    /// <param name="IgnoreFailedSources"> ignore FatalProtocolException, the same as NuGet
    /// https://github.com/NuGet/NuGet.Client/blob/f24bad0668193ce21a1db8cabd1ce95ba509c7f0/src/NuGet.Core/NuGet.Protocol/RemotePackageArchiveDownloader.cs</param>
    internal record RestoreActionConfig(
        bool DisableParallel = false,
        bool NoCache = false,
        bool IgnoreFailedSources = false,
        bool Interactive = false);
}
