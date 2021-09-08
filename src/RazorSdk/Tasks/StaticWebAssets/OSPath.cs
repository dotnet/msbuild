// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Razor
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
