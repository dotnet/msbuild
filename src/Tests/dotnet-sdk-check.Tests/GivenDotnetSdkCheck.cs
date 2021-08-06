// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.NativeWrapper;
using Microsoft.DotNet.Tools.Sdk.Check;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.SdkCheck.Tests
{
    public class GivenDotnetSdkCheck : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string fakeReleasesPath;

        private const string HelpText = @"Description:
      .NET SDK Check Command
    
    Usage:
      dotnet [options] sdk check
    
    Options:
      -?, -h, --help    Show command line help.";

        public GivenDotnetSdkCheck(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            fakeReleasesPath = Path.Combine(_testAssetsManager.TestAssetsRoot, "TestReleases", "TestRelease");
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sdk", "check", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WhenNewFeatureBandExistsItIsAdvertised(bool newerBandExists)
        {
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "sdk", "check" });
            var oldSdks = GetFakeEnvironmentInfo(new[] { "3.1.100" }, Array.Empty<string>());
            var newSdks = GetFakeEnvironmentInfo(new[] { "5.0.100" }, Array.Empty<string>());

            new SdkCheckCommand(parseResult, new MockNETBundleProvider(newerBandExists ? oldSdks : newSdks), new MockProductCollectionProvider(fakeReleasesPath), _reporter).Execute();

            if (newerBandExists)
            {
                _reporter.Lines
                    .Should()
                    .Contain(new[] { string.Format(LocalizableStrings.NewFeatureBandMessage, "5.0.100") });
            }
            else
            {
                string.Join(' ', _reporter.Lines)
                    .Should()
                    .NotContain(LocalizableStrings.NewFeatureBandMessage.Replace(".NET {0}.", string.Empty));
            }
        }

        [Fact]
        public void ItContainsInfoForAllInstalledBundles()
        {
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "sdk", "check" });
            var bundles = GetFakeEnvironmentInfo(new[] { "1.0.10", "2.1.809", "3.1.402", "5.0.100" }, new[] { "1.1.4", "2.1.8", "3.1.0", "3.1.3", "5.0.0" });

            new SdkCheckCommand(parseResult, new MockNETBundleProvider(bundles), new MockProductCollectionProvider(fakeReleasesPath), _reporter).Execute();

            foreach (var version in bundles.SdkInfo.Select(b => b.Version.ToString()))
            {
                string.Join(' ', _reporter.Lines)
                    .Should()
                    .Contain(version);
            }

            foreach (var bundle in bundles.RuntimeInfo)
            {
                string.Join(' ', _reporter.Lines)
                    .Should()
                    .Contain(bundle.Version.ToString());
                string.Join(' ', _reporter.Lines)
                    .Should()
                    .Contain(bundle.Name.ToString());
            }
        }

        [Theory]
        [InlineData(new string[] { "3.1.301" }, new string[] { }, new string[] { "3.1.302" })]
        [InlineData(new string[] { "5.0.100" }, new string[] { }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "3.1.3" }, new string[] { "3.1.10" })]
        [InlineData(new string[] { }, new string[] { "5.0.0" }, new string[] { })]
        [InlineData(new string[] { "1.1.10", "2.1.300", "2.1.810", "3.1.400" }, new string[] { }, new string[] { "2.1.302", "2.1.811", "3.1.404" })]
        [InlineData(new string[] { }, new string[] { "1.1.10", "2.1.20", "3.1.0" }, new string[] { "2.1.23", "3.1.10" })]
        [InlineData(new string[] { "1.1.10", "2.1.300", "2.1.810", "3.1.400" }, new string[] { "1.1.10", "2.1.20", "3.1.0" }, new string[] { "2.1.302", "2.1.811", "3.1.404", "2.1.23", "3.1.10" })]
        public void WhenANewPatchIsAvailableItIsAdvertised(string[] sdkVersions, string[] runtimeVersions, string[] latestPatchVersions)
        {
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "sdk", "check" });
            var bundles = GetFakeEnvironmentInfo(sdkVersions, runtimeVersions);

            new SdkCheckCommand(parseResult, new MockNETBundleProvider(bundles), new MockProductCollectionProvider(fakeReleasesPath), _reporter).Execute();

            var commandResult = string.Join(' ', _reporter.Lines);
            var expectedLines = latestPatchVersions.Select(version => string.Format(LocalizableStrings.NewPatchAvailableMessage, version));
            foreach (var line in expectedLines)
            {
                commandResult
                    .Should()
                    .Contain(line);
            }
        }

        [Theory]
        [InlineData(new string[] { "1.0.10" }, new string[] { }, new string[] { "1.0.10" })]
        [InlineData(new string[] { "5.0.100" }, new string[] { }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "1.0.1" }, new string[] { "1.0.1" })]
        [InlineData(new string[] { }, new string[] { "5.0.0" }, new string[] { })]
        [InlineData(new string[] { "1.0.10", "1.0.9", "2.0.308", "2.1.804", "3.0.309", "3.1.401" }, new string[] { }, new string[] { "1.0.10", "1.0.9", "2.0.308" })]
        [InlineData(new string[] { }, new string[] { "1.0.0", "1.0.1", "2.0.3", "2.1.8", "3.0.3", "3.1.4" }, new string[] { "1.0.0", "1.0.1", "2.0.3" })]
        [InlineData(new string[] { "1.0.10", "1.0.9", "2.0.308", "2.1.804", "3.0.309", "3.1.401" }, new string[] { "1.0.0", "1.0.1", "2.0.3", "2.1.8", "3.0.3", "3.1.4" },
            new string[] { "1.0.10", "1.0.9", "2.0.308", "1.0.0", "1.0.1", "2.0.3" })]
        public void WhenABundleIsOutOfSupportItPrintsWarning(string[] sdkVersions, string[] runtimeVersions, string[] outOfSupportVersions)
        {
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "sdk", "check" });
            var bundles = GetFakeEnvironmentInfo(sdkVersions, runtimeVersions);

            new SdkCheckCommand(parseResult, new MockNETBundleProvider(bundles), new MockProductCollectionProvider(fakeReleasesPath), _reporter).Execute();

            var commandResult = string.Join(' ', _reporter.Lines);
            var expectedLines = outOfSupportVersions.Select(version => string.Format(LocalizableStrings.OutOfSupportMessage, version.Substring(0, 3)));
            foreach (var line in expectedLines)
            {
                commandResult
                    .Should()
                    .Contain(line);
            }

            var unexpectedLines = sdkVersions.Concat(runtimeVersions).Except(outOfSupportVersions)
                .Select(version => string.Format(LocalizableStrings.OutOfSupportMessage, version.Substring(0, 3)));
            foreach (var line in unexpectedLines)
            {
                commandResult
                    .Should()
                    .NotContain(line);
            }
        }

        [Theory]
        [InlineData(new string[] { "3.0.100" }, new string[] { }, new string[] { "3.0.100" })]
        [InlineData(new string[] { "5.0.100" }, new string[] { }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "3.0.1" }, new string[] { "3.0.1" })]
        [InlineData(new string[] { }, new string[] { "5.0.0" }, new string[] { })]
        [InlineData(new string[] { "1.0.10", "2.0.308", "3.0.309", "3.0.100", "3.1.401" }, new string[] { }, new string[] { "3.0.309", "3.0.100" })]
        [InlineData(new string[] { }, new string[] { "1.0.1", "2.0.3", "3.0.3", "3.0.1", "3.1.4" }, new string[] { "3.0.3", "3.0.1" })]
        [InlineData(new string[] { "1.0.10", "2.0.308", "3.0.309", "3.0.100", "3.1.401" }, new string[] { "1.0.1", "2.0.3", "3.0.3", "3.0.1", "3.1.4" }, new string[] { "3.0.309", "3.0.100", "3.0.3", "3.0.1" })]
        public void WhenABundleIsInMaintenanceModeItPrintsWarning(string[] sdkVersions, string[] runtimeVersions, string[] maintenanceVersions)
        {
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "sdk", "check" });
            var bundles = GetFakeEnvironmentInfo(sdkVersions, runtimeVersions);

            new SdkCheckCommand(parseResult, new MockNETBundleProvider(bundles), new MockProductCollectionProvider(fakeReleasesPath), _reporter).Execute();

            var commandResult = string.Join('\n', _reporter.Lines);
            var expectedLines = maintenanceVersions.Select(version => string.Format(LocalizableStrings.MaintenanceMessage, version.Substring(0, 3)));
            foreach (var line in expectedLines)
            {
                commandResult
                    .Should()
                    .Contain(line);
            }

            var unexpectedLines = sdkVersions.Concat(runtimeVersions).Except(maintenanceVersions)
                .Select(version => string.Format(LocalizableStrings.MaintenanceMessage, version.Substring(0, 3)));
            foreach (var line in unexpectedLines)
            {
                commandResult
                    .Should()
                    .NotContain(line);
            }
        }

        private NetEnvironmentInfo GetFakeEnvironmentInfo(IEnumerable<string> sdkVersions, IEnumerable<string> runtimeVersions)
        {
            var sdks = sdkVersions.Select(version => new NetSdkInfo(version, string.Empty));
            var runtimes = runtimeVersions.Select(version => new NetRuntimeInfo("FakeName", version, string.Empty));
            return new NetEnvironmentInfo(runtimes, sdks);
        }
    }
}
