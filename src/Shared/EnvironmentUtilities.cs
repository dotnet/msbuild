// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Shared
{
    internal static partial class EnvironmentUtilities
    {
        public static bool Is64BitProcess => Marshal.SizeOf<IntPtr>() == 8;

        public static bool Is64BitOperatingSystem =>
            Environment.Is64BitOperatingSystem;

        public static bool IsWellKnownEnvironmentDerivedProperty(string propertyName)
        {
            return propertyName.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) ||
                propertyName.StartsWith("COMPLUS_", StringComparison.OrdinalIgnoreCase) ||
                propertyName.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
