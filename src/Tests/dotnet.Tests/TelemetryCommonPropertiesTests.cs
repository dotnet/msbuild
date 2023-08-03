// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Tests
{
    public class TelemetryCommonPropertiesTests : SdkTest
    {
        public TelemetryCommonPropertiesTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldContainIfItIsInDockerOrNot()
        {
            var unitUnderTest = new TelemetryCommonProperties(userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties().Should().ContainKey("Docker Container");
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldReturnHashedPath()
        {
            var unitUnderTest = new TelemetryCommonProperties(() => "ADirectory", userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Current Path Hash"].Should().NotBe("ADirectory");
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldReturnHashedMachineId()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => "plaintext", userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Machine ID"].Should().NotBe("plaintext");
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldReturnNewGuidWhenCannotGetMacAddress()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            var assignedMachineId = unitUnderTest.GetTelemetryCommonProperties()["Machine ID"];

            Guid.TryParse(assignedMachineId, out var _).Should().BeTrue("it should be a guid");
        }
        
        [Fact]
        public void TelemetryCommonPropertiesShouldReturnHashedMachineIdOld()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => "plaintext", userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Machine ID Old"].Should().NotBe("plaintext");
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldReturnNewGuidWhenCannotGetMacAddressOld()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            var assignedMachineId = unitUnderTest.GetTelemetryCommonProperties()["Machine ID Old"];

            Guid.TryParse(assignedMachineId, out var _).Should().BeTrue("it should be a guid");
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldReturnIsOutputRedirected()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Output Redirected"].Should().BeOneOf("True", "False");
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldReturnIsCIDetection()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Continuous Integration"].Should().BeOneOf("True", "False");
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldContainKernelVersion()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Kernel Version"].Should().Be(RuntimeInformation.OSDescription);
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldContainArchitectureInformation()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["OS Architecture"].Should().Be(RuntimeInformation.OSArchitecture.ToString());
        }

        [WindowsOnlyFact]
        public void TelemetryCommonPropertiesShouldContainWindowsInstallType()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Installation Type"].Should().NotBeEmpty();
        }

        [UnixOnlyFact]
        public void TelemetryCommonPropertiesShouldContainEmptyWindowsInstallType()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Installation Type"].Should().BeEmpty();
        }

        [WindowsOnlyFact]
        public void TelemetryCommonPropertiesShouldContainWindowsProductType()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Product Type"].Should().NotBeEmpty();
        }

        [UnixOnlyFact]
        public void TelemetryCommonPropertiesShouldContainEmptyWindowsProductType()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Product Type"].Should().BeEmpty();
        }

        [WindowsOnlyFact]
        public void TelemetryCommonPropertiesShouldContainEmptyLibcReleaseAndVersion()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Libc Release"].Should().BeEmpty();
            unitUnderTest.GetTelemetryCommonProperties()["Libc Version"].Should().BeEmpty();
        }

        [MacOsOnlyFact]
        public void TelemetryCommonPropertiesShouldContainEmptyLibcReleaseAndVersion2()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Libc Release"].Should().BeEmpty();
            unitUnderTest.GetTelemetryCommonProperties()["Libc Version"].Should().BeEmpty();
        }

        [LinuxOnlyFact]
        public void TelemetryCommonPropertiesShouldContainLibcReleaseAndVersion()
        {
            if (!RuntimeInformation.RuntimeIdentifier.Contains("alpine", StringComparison.OrdinalIgnoreCase))
            {
                var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
                unitUnderTest.GetTelemetryCommonProperties()["Libc Release"].Should().NotBeEmpty();
                unitUnderTest.GetTelemetryCommonProperties()["Libc Version"].Should().NotBeEmpty();
            }
        }

        [Theory]
        [MemberData(nameof(CITelemetryTestCases))]
        public void CanDetectCIStatusForEnvVars(Dictionary<string, string> envVars, bool expected) {
            try {
                foreach (var (key, value) in envVars) {
                    Environment.SetEnvironmentVariable(key, value);
                }
                new CIEnvironmentDetectorForTelemetry().IsCIEnvironment().Should().Be(expected);
            } finally {
                foreach (var (key, value) in envVars) {
                    Environment.SetEnvironmentVariable(key, null);
                }
            }
        }

        public static IEnumerable<object[]> CITelemetryTestCases => new List<object[]>{
            new object[] { new Dictionary<string, string> { { "TF_BUILD", "true" } }, true },
            new object[] { new Dictionary<string, string> { { "GITHUB_ACTIONS", "true" } }, true },
            new object[] { new Dictionary<string, string> { { "APPVEYOR", "true"} }, true },
            new object[] { new Dictionary<string, string> { { "CI", "true"} }, true },
            new object[] { new Dictionary<string, string> { { "TRAVIS", "true"} }, true },
            new object[] { new Dictionary<string, string> { { "CIRCLECI", "true"} }, true },

            new object[] { new Dictionary<string, string> { { "CODEBUILD_BUILD_ID", "hi" }, { "AWS_REGION", "hi" } }, true },
            new object[] { new Dictionary<string, string> { { "CODEBUILD_BUILD_ID", "hi" } }, false },
            new object[] { new Dictionary<string, string> { { "BUILD_ID", "hi" }, { "BUILD_URL", "hi" } }, true },
            new object[] { new Dictionary<string, string> { { "BUILD_ID", "hi" } }, false },
            new object[] { new Dictionary<string, string> { { "BUILD_ID", "hi" }, { "PROJECT_ID", "hi" } }, true },
            new object[] { new Dictionary<string, string> { { "BUILD_ID", "hi" } }, false },

            new object[] { new Dictionary<string, string> { { "TEAMCITY_VERSION", "hi" } }, true },
            new object[] { new Dictionary<string, string> { { "TEAMCITY_VERSION", "" } }, false },
            new object[] { new Dictionary<string, string> { { "JB_SPACE_API_URL", "hi" } }, true },
            new object[] { new Dictionary<string, string> { { "JB_SPACE_API_URL", "" } }, false },

            new object[] { new Dictionary<string, string> { { "SomethingElse", "hi" } }, false },
        };

        private class NothingCache : IUserLevelCacheWriter
        {
            public string RunWithCache(string cacheKey, Func<string> getValueToCache)
            {
                return getValueToCache();
            }

            public string RunWithCacheInFilePath(string cacheFilepath, Func<string> getValueToCache)
            {
                return getValueToCache();
            }
        }
    }
}
