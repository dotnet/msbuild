// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet
{
    class OSVersionUtil
    {
        public static bool IsWindows8OrNewer()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                return false;
            }

            if (!Version.TryParse(RuntimeEnvironment.OperatingSystemVersion, out var winVersion))
            {
                // All current versions of Windows have a valid System.Version value for OperatingSystemVersion.
                // If parsing fails, let's assume Windows is newer than Win 8.
                return true;
            }

            // Windows 7 = "6.1"
            // Windows 8 = "6.2"
            // Windows 8.1 = "6.3"
            if (winVersion.Major > 6)
            {
                return true;
            }

            return winVersion.Minor >= 2;
        }
    }
}
