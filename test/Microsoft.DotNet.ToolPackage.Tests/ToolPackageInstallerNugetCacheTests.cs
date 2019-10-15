// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using NuGet.Versioning;
using Microsoft.DotNet.Tools.Tests.Utilities;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class ToolPackageInstallToManagedLocationInstaller : TestBase
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            var nugetCacheLocation =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            IToolPackage toolPackage = installer.InstallPackageToExternalManagedLocation(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                packageLocation : new PackageLocation(nugetConfig: nugetConfigPath),
                targetFramework: _testTargetframework);

            var commands = toolPackage.Commands;
            commands[0].Executable.Value.Should().StartWith(NuGetGlobalPackagesFolder.GetLocation());

            fileSystem.File
                .Exists(commands[0].Executable.Value)
                .Should().BeTrue($"{commands[0].Executable.Value} should exist");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigVersionRangeInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            var nugetCacheLocation =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            IToolPackage toolPackage = installer.InstallPackageToExternalManagedLocation(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse("1.0.0-*"),
                packageLocation: new PackageLocation(nugetConfig: nugetConfigPath),
                targetFramework: _testTargetframework);

            var commands = toolPackage.Commands;
            commands[0].Executable.Value.Should().StartWith(NuGetGlobalPackagesFolder.GetLocation());
            toolPackage.Version.Should().Be(NuGetVersion.Parse(TestPackageVersion));
        }

        private static FilePath GetUniqueTempProjectPathEachTest()
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());
            var tempProjectPath =
                tempProjectDirectory.WithFile(Path.GetRandomFileName() + ".csproj");
            return tempProjectPath;
        }

        private static List<MockFeed> GetMockFeedsForConfigFile(FilePath nugetConfig)
        {
            return new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.ExplicitNugetConfig,
                    Uri = nugetConfig.Value,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = TestPackageId.ToString(),
                            Version = TestPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        }
                    }
                }
            };
        }

        private static (IToolPackageStore, IToolPackageInstaller, BufferedReporter, IFileSystem) Setup(
            bool useMock,
            List<MockFeed> feeds = null,
            FilePath? tempProject = null,
            DirectoryPath? offlineFeed = null)
        {
            var root = new DirectoryPath(Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName()));
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageInstaller installer;
            if (useMock)
            {
                fileSystem = new FileSystemMockBuilder().Build();
                store = new ToolPackageStoreMock(root, fileSystem);
                installer = new ToolPackageInstallerMock(
                    fileSystem: fileSystem,
                    store: store,
                    projectRestorer: new ProjectRestorerMock(
                        fileSystem: fileSystem,
                        reporter: reporter,
                        feeds: feeds));
            }
            else
            {
                fileSystem = new FileSystemWrapper();
                store = new ToolPackageStoreAndQuery(root);
                installer = new ToolPackageInstaller(
                    store: store,
                    projectRestorer: new ProjectRestorer(reporter),
                    tempProject: tempProject ?? GetUniqueTempProjectPathEachTest(),
                    offlineFeed: offlineFeed ?? new DirectoryPath("does not exist"));
            }

            return (store, installer, reporter, fileSystem);
        }

        private static FilePath WriteNugetConfigFileToPointToTheFeed()
        {
            var nugetConfigName = "nuget.config";

            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(Path.GetTempPath(),
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());
            Directory.CreateDirectory(tempPathForNugetConfigWithWhiteSpace);

            NuGetConfig.Write(
                directory: tempPathForNugetConfigWithWhiteSpace,
                configname: nugetConfigName,
                localFeedPath: GetTestLocalFeedPath());

            return new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
        }

        private static string GetTestLocalFeedPath() => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");
        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");
    }
}
