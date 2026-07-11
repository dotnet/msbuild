// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Shouldly;


namespace Microsoft.Build.Framework.UnitTests
{
    [TestClass]
    public class OperatingSystem_Tests
    {
#if !NET5_0_OR_GREATER
        [WindowsFullFrameworkOnlyTheory]
        [DataRow("windows", true)]
        [DataRow("linux", false)]
        [DataRow("macOS", false)]
        public void IsOSPlatform(string platform, bool expected)
        {
            Microsoft.Build.Framework.OperatingSystem.IsOSPlatform(platform).ShouldBe(expected);
        }

        [WindowsFullFrameworkOnlyTheory]
        [DataRow("windows", 4, true)]
        [DataRow("windows", 999, false)]
        [DataRow("linux", 0, false)]
        [DataRow("macOS", 0, false)]
        public void IsOSPlatformVersionAtLeast(string platform, int major, bool expected)
        {
            Microsoft.Build.Framework.OperatingSystem.IsOSPlatformVersionAtLeast(platform, major).ShouldBe(expected);
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsWindows()
        {
            Microsoft.Build.Framework.OperatingSystem.IsWindows().ShouldBeTrue();
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsWindowsVersionAtLeast()
        {
            Microsoft.Build.Framework.OperatingSystem.IsWindowsVersionAtLeast(4).ShouldBeTrue();
            Microsoft.Build.Framework.OperatingSystem.IsWindowsVersionAtLeast(999).ShouldBeFalse();
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsOtherThanWindows()
        {
            Microsoft.Build.Framework.OperatingSystem.IsFreeBSD().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsFreeBSDVersionAtLeast(0).ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsLinux().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsMacOS().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsMacOSVersionAtLeast(0).ShouldBeFalse();
        }
#endif
    }
}
