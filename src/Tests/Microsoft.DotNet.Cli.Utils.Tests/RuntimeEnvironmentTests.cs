// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

            var osRelease = File.ReadAllLines("/etc/os-release");
            string id = osRelease
                .First(line => line.StartsWith("ID=", StringComparison.OrdinalIgnoreCase))
                .Substring("ID=".Length)
                .Trim('\"', '\'')
                .ToLowerInvariant();
            Assert.Equal(id, RuntimeEnvironment.OperatingSystem.ToLowerInvariant());

            string version = osRelease
                .First(line => line.StartsWith("VERSION_ID=", StringComparison.OrdinalIgnoreCase))
                .Substring("VERSION_ID=".Length)
                .Trim('\"', '\'');
            Assert.Equal(version, RuntimeEnvironment.OperatingSystemVersion);
        }
    }
}
