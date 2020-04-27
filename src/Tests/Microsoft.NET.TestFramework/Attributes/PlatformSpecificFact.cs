// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class PlatformSpecificFact : FactAttribute
    {
        public PlatformSpecificFact(TestPlatforms platforms)
        {
            if (ShouldSkip(platforms))
            {
                this.Skip = "This test is not supported on this platform.";
            }
        }

        internal static bool ShouldSkip(TestPlatforms platforms) =>
            (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !platforms.HasFlag(TestPlatforms.Windows))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !platforms.HasFlag(TestPlatforms.Linux))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !platforms.HasFlag(TestPlatforms.OSX))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")) && !platforms.HasFlag(TestPlatforms.FreeBSD));
    }
}
