// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class RuntimeEnvironmentTests : SdkTest
    {
        public RuntimeEnvironmentTests(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void VerifyWindows()
        {
            Assert.Equal(Platform.Windows, RuntimeEnvironment.OperatingSystemPlatform);
            Assert.Equal("Windows", RuntimeEnvironment.OperatingSystem);

            Version osVersion = Version.Parse(RuntimeEnvironment.OperatingSystemVersion);
            Version expectedOSVersion = Environment.OSVersion.Version;

            // 3 parts of the version should be supplied for Windows
            Assert.Equal(expectedOSVersion.Major, osVersion.Major);
            Assert.Equal(expectedOSVersion.Minor, osVersion.Minor);
            Assert.Equal(expectedOSVersion.Build, osVersion.Build);
            Assert.Equal(-1, osVersion.Revision);
        }

        [MacOsOnlyFact]
        public void VerifyMacOs()
        {
            Assert.Equal(Platform.Darwin, RuntimeEnvironment.OperatingSystemPlatform);
            Assert.Equal("Mac OS X", RuntimeEnvironment.OperatingSystem);

            Version osVersion = Version.Parse(RuntimeEnvironment.OperatingSystemVersion);
            Version expectedOSVersion = Environment.OSVersion.Version;

            // 2 parts of the version should be supplied for macOS
            Assert.Equal(expectedOSVersion.Major, osVersion.Major);
            Assert.Equal(expectedOSVersion.Minor, osVersion.Minor);
            Assert.Equal(-1, osVersion.Build);
            Assert.Equal(-1, osVersion.Revision);
        }

        [LinuxOnlyFact]
        public void VerifyLinux()
        {
            Assert.Equal(Platform.Linux, RuntimeEnvironment.OperatingSystemPlatform);

            // ensure OperatingSystem and OperatingSystemVersion are aligned with the current RID
            Assert.StartsWith(
                $"{RuntimeEnvironment.OperatingSystem.ToLowerInvariant()}.{RuntimeEnvironment.OperatingSystemVersion}",
                RuntimeInformation.RuntimeIdentifier);
        }
    }
}
