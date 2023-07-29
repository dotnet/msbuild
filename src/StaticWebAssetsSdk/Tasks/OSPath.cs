// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    internal static class OSPath
    {
        public static StringComparer PathComparer { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase :
            StringComparer.Ordinal;

        public static StringComparison PathComparison { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase :
            StringComparison.Ordinal;
    }
}
