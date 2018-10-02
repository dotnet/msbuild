// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Shared
{
    internal static partial class EnvironmentUtilities
    {
        public static bool Is64BitProcess => Marshal.SizeOf<IntPtr>() == 8;

        public static bool Is64BitOperatingSystem =>
#if FEATURE_64BIT_ENVIRONMENT_QUERY
            Environment.Is64BitOperatingSystem;
#else
            RuntimeInformation.OSArchitecture == Architecture.Arm64 ||
            RuntimeInformation.OSArchitecture == Architecture.X64;
#endif
    }
}
