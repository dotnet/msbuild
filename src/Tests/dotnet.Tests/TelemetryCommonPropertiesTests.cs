// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;
using System;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

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
        public void TelemetryCommonPropertiesShouldReturnIsOutputRedirected()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Output Redirected"].Should().BeOneOf("True", "False");
        }

        [Fact]
        public void TelemetryCommonPropertiesShouldContainKernelVersion()
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties()["Kernel Version"].Should().Be(RuntimeInformation.OSDescription);
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
