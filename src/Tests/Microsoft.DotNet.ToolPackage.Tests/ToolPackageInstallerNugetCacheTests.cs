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
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class ToolPackageInstallToManagedLocationInstaller : SdkTest
    {
        public ToolPackageInstallToManagedLocationInstaller(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            string testDirectory = _testAssetsManager.CreateTestDirectory(identifier: testMockBehaviorIsInSync.ToString()).Path;

            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed(testDirectory);

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                testDirectory: testDirectory,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            try

            {
                var nugetCacheLocation =
                    new DirectoryPath(testDirectory).WithSubDirectories(Path.GetRandomFileName());

                IToolPackage toolPackage = installer.InstallPackageToExternalManagedLocation(
                    packageId: TestPackageId,
                    versionRange: VersionRange.Parse(TestPackageVersion),
                    packageLocation: new PackageLocation(nugetConfig: nugetConfigPath),
                    targetFramework: _testTargetframework);

                var commands = toolPackage.Commands;
                var expectedPackagesFolder = testMockBehaviorIsInSync ?
                            NuGetGlobalPackagesFolder.GetLocation() :
                            TestContext.Current.NuGetCachePath;
                commands[0].Executable.Value.Should().StartWith(expectedPackagesFolder);

                fileSystem.File
                    .Exists(commands[0].Executable.Value)
                    .Should().BeTrue($"{commands[0].Executable.Value} should exist");
            }
            finally
            {
                foreach (var line in reporter.Lines)
                {
                    Log.WriteLine(line);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigVersionRangeInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            string testDirectory = _testAssetsManager.CreateTestDirectory(identifier: testMockBehaviorIsInSync.ToString()).Path;

            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed(testDirectory);

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                testDirectory: testDirectory,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            var nugetCacheLocation =
                new DirectoryPath(testDirectory).WithSubDirectories(Path.GetRandomFileName());

            IToolPackage toolPackage = installer.InstallPackageToExternalManagedLocation(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse("1.0.0-*"),
                packageLocation: new PackageLocation(nugetConfig: nugetConfigPath),
                targetFramework: _testTargetframework);

            var expectedPackagesFolder = testMockBehaviorIsInSync ?
                            NuGetGlobalPackagesFolder.GetLocation() :
                            TestContext.Current.NuGetCachePath;

            var commands = toolPackage.Commands;
            commands[0].Executable.Value.Should().StartWith(expectedPackagesFolder);
            toolPackage.Version.Should().Be(NuGetVersion.Parse(TestPackageVersion));
        }

        private static FilePath GetUniqueTempProjectPathEachTest(string testDirectory)
        {
            var tempProjectDirectory =
                new DirectoryPath(testDirectory).WithSubDirectories(Path.GetRandomFileName());
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

        private (IToolPackageStore, IToolPackageInstaller, BufferedReporter, IFileSystem) Setup(
            bool useMock,
            string testDirectory,
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
                    projectRestorer: new Stage2ProjectRestorer(Log, reporter),
                    tempProject: tempProject ?? GetUniqueTempProjectPathEachTest(testDirectory),
                    offlineFeed: offlineFeed ?? new DirectoryPath("does not exist"));
            }

            return (store, installer, reporter, fileSystem);
        }

        private FilePath WriteNugetConfigFileToPointToTheFeed(string testDirectory)
        {
            var nugetConfigName = "NuGet.Config";

            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(testDirectory,
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());
            Directory.CreateDirectory(tempPathForNugetConfigWithWhiteSpace);

            NuGetConfigWriter.Write(tempPathForNugetConfigWithWhiteSpace, GetTestLocalFeedPath());

            return new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
        }

        private static string GetTestLocalFeedPath() => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");
        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");
    }
}
