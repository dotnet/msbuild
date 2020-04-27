// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

            Assert.Equal(expectedOSVersion.Major, osVersion.Major);
            Assert.Equal(expectedOSVersion.Minor, osVersion.Minor);
            Assert.Equal(expectedOSVersion.Build, osVersion.Build);
        }

        [MacOsOnlyFact]
        public void VerifyMacOs()
        {
            Assert.Equal(Platform.Darwin, RuntimeEnvironment.OperatingSystemPlatform);
            Assert.Equal("Mac OS X", RuntimeEnvironment.OperatingSystem);

            Version osVersion = Version.Parse(RuntimeEnvironment.OperatingSystemVersion);

            Assert.Equal(10, osVersion.Major);
            Assert.Equal(Environment.OSVersion.Version.Major - 4, osVersion.Minor);
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
