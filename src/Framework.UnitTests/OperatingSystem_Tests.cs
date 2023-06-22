// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Shouldly;

using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.Framework.UnitTests
{
    public class OperatingSystem_Tests
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Don't complain when test body is empty")]
        [WindowsFullFrameworkOnlyTheory]
        [InlineData("windows", true)]
        [InlineData("linux", false)]
        [InlineData("macOS", false)]
        public void IsOSPlatform(string platform, bool expected)
        {
#if !NET5_0_OR_GREATER
            Microsoft.Build.Framework.OperatingSystem.IsOSPlatform(platform).ShouldBe(expected);
#endif
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsWindows()
        {
#if !NET5_0_OR_GREATER
            Microsoft.Build.Framework.OperatingSystem.IsWindows().ShouldBeTrue();
#endif
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsWindowsVersionAtLeast()
        {
#if !NET5_0_OR_GREATER
            Microsoft.Build.Framework.OperatingSystem.IsWindowsVersionAtLeast(4).ShouldBeTrue();
#endif
        }

        [WindowsFullFrameworkOnlyFact]
        public void IsOtherThanWindows()
        {
#if !NET5_0_OR_GREATER
            Microsoft.Build.Framework.OperatingSystem.IsAndroid().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsAndroidVersionAtLeast(0).ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsBrowser().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsFreeBSD().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsFreeBSDVersionAtLeast(0).ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsIOS().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsIOSVersionAtLeast(0).ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsLinux().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsMacCatalyst().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsMacCatalystVersionAtLeast(0).ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsMacOS().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsMacOSVersionAtLeast(0).ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsOSXLike().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsTvOS().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsTvOSVersionAtLeast(0).ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsWasi().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsWatchOS().ShouldBeFalse();
            Microsoft.Build.Framework.OperatingSystem.IsWatchOSVersionAtLeast(0).ShouldBeFalse();
#endif
        }
    }
}
