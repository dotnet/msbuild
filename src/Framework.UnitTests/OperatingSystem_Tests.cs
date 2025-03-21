// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Shouldly;

using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.Framework.UnitTests
{
    public class OperatingSystem_Tests
    {
#if !NET5_0_OR_GREATER
        [WindowsFullFrameworkOnlyTheory]
        [InlineData("windows", true)]
        [InlineData("linux", false)]
        [InlineData("macOS", false)]
        public void IsOSPlatform(string platform, bool expected)
        {
            OperatingSystem.IsOSPlatform(platform).ShouldBe(expected);
        }

        [WindowsFullFrameworkOnlyTheory]
        [InlineData("windows", 4, true)]
        [InlineData("windows", 999, false)]
        [InlineData("linux", 0, false)]
        [InlineData("macOS", 0, false)]
        public void IsOSPlatformVersionAtLeast(string platform, int major, bool expected)
        {
            OperatingSystem.IsOSPlatformVersionAtLeast(platform, major).ShouldBe(expected);
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsWindows()
        {
            OperatingSystem.IsWindows().ShouldBeTrue();
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsWindowsVersionAtLeast()
        {
            OperatingSystem.IsWindowsVersionAtLeast(4).ShouldBeTrue();
            OperatingSystem.IsWindowsVersionAtLeast(999).ShouldBeFalse();
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsOtherThanWindows()
        {
            OperatingSystem.IsFreeBSD().ShouldBeFalse();
            OperatingSystem.IsFreeBSDVersionAtLeast(0).ShouldBeFalse();
            OperatingSystem.IsLinux().ShouldBeFalse();
            OperatingSystem.IsMacOS().ShouldBeFalse();
            OperatingSystem.IsMacOSVersionAtLeast(0).ShouldBeFalse();
        }
#endif
    }
}
