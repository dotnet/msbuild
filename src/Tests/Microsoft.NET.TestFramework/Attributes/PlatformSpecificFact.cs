// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
