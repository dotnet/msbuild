// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace ManifestReaderTests
{

    public class SdkFeatureBandTests : SdkTest
    {
        public SdkFeatureBandTests(ITestOutputHelper logger) : base(logger)
        {
        }

        [Theory]
        [InlineData("6.0.100", "6.0.100")]
        [InlineData("10.0.512", "10.0.500")]
        [InlineData("7.0.100-preview.1.12345", "7.0.100-preview.1")]
        [InlineData("7.0.100-dev", "7.0.100")]
        [InlineData("7.0.100-ci", "7.0.100")]
        [InlineData("6.0.100-rc.2.21505.57", "6.0.100-rc.2")]
        [InlineData("7.0.100-alpha.1.21558.2", "7.0.100-alpha.1")]
        public void ItParsesVersionsCorrectly(string version, string expectedParsedVersion)
        {
            var parsedVersion = new SdkFeatureBand(version).ToString();
            parsedVersion.Should().Be(expectedParsedVersion);
        }

        [Theory]
        [InlineData("6.0.100", "6.0.100")]
        [InlineData("10.0.512", "10.0.500")]
        [InlineData("7.0.105-preview.1.12345", "7.0.100")]
        [InlineData("7.0.100-dev", "7.0.100")]
        [InlineData("7.0.100-ci", "7.0.100")]
        [InlineData("6.0.100-rc.2.21505.57", "6.0.100")]
        [InlineData("7.0.400-alpha.1.21558.2", "7.0.400")]
        public void ItDiscardsPreleaseLabelsCorrectly(string version, string expectedParsedVersion)
        {
            var parsedVersion = new SdkFeatureBand(version).ToStringWithoutPrerelease();
            parsedVersion.Should().Be(expectedParsedVersion);
        }
    }
}
