// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Shared
{
    internal static partial class EnvironmentUtilities
    {
        public static bool Is64BitProcess => Marshal.SizeOf<IntPtr>() == 8;

        public static bool Is64BitOperatingSystem =>
            Environment.Is64BitOperatingSystem;

        public static Dictionary<string, string> EnvironmentVariablesUsedAsProperties { get; } = new(MSBuildNameIgnoreCaseComparer.Default);
    }
}
