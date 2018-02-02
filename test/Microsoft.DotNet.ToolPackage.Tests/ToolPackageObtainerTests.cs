// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Install.Tool;
using Xunit;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class ToolPackageObtainerTests : TestBase
    {
        [Fact]
        public void GivenNoFeedItThrows()
        {
            var reporter = new BufferedReporter();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            ToolPackageObtainer packageObtainer =
                new ToolPackageObtainer(
                new DirectoryPath(toolsPath),
                new DirectoryPath("no such path"),
                GetUniqueTempProjectPathEachTest,
                new Lazy<string>(),
                new ProjectRestorer(reporter));

            Action a = () => packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                targetframework: _testTargetframework);

            a.ShouldThrow<PackageObtainException>();

            reporter.Lines.Count.Should().Be(1);
            reporter.Lines[0].Should().Contain(TestPackageId);
        }

        [Fact]
        public void GivenOfflineFeedWhenCallItCanDownloadThePackage()
        {
            var reporter = new BufferedReporter();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            ToolPackageObtainer packageObtainer =
                new ToolPackageObtainer(
                    toolsPath: new DirectoryPath(toolsPath),
                    offlineFeedPath: new DirectoryPath(GetTestLocalFeedPath()),
                    getTempProjectPath: GetUniqueTempProjectPathEachTest,
                    bundledTargetFrameworkMoniker: new Lazy<string>(),
                    projectRestorer: new ProjectRestorer(reporter));

            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    targetframework: _testTargetframework);

            reporter.Lines.Should().BeEmpty();

            var executable = toolConfigurationAndExecutablePath
                .Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");

            executable.Value.Should().NotContain(GetTestLocalFeedPath(), "Executable should not be still in fallbackfolder");
            executable.Value.Should().Contain(toolsPath, "Executable should be copied to tools Path");

            File.Delete(executable.Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigAndPackageNameAndVersionAndTargetFrameworkWhenCallItCanDownloadThePackage(
            bool testMockBehaviorIsInSync)
        {
            var reporter = new BufferedReporter();
            FilePath nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath, reporter, testMockBehaviorIsInSync, nugetConfigPath.Value);

            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath
                = packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    nugetconfig: nugetConfigPath,
                    targetframework: _testTargetframework);

            reporter.Lines.Should().BeEmpty();

            FilePath executable = toolConfigurationAndExecutablePath.Executable;
            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");

            File.Delete(executable.Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigAndPackageNameAndVersionAndTargetFrameworkWhenCallItCreateAssetFile(
            bool testMockBehaviorIsInSync)
        {
            var reporter = new BufferedReporter();
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath, reporter, testMockBehaviorIsInSync, nugetConfigPath.Value);

            ToolConfigurationAndExecutablePath toolConfigurationAndExecutableDirectory =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    nugetconfig: nugetConfigPath,
                    targetframework: _testTargetframework);

            reporter.Lines.Should().BeEmpty();

            /*
              From mytool.dll to project.assets.json
               .dotnet/.tools/packageid/version/packageid/version/mytool.dll
                      /dependency1 package id/
                      /dependency2 package id/
                      /project.assets.json
             */
            var assetJsonPath = toolConfigurationAndExecutableDirectory
                .Executable
                .GetDirectoryPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .WithFile("project.assets.json").Value;

            File.Exists(assetJsonPath)
                .Should()
                .BeTrue(assetJsonPath + " should be created");

            File.Delete(assetJsonPath);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoNugetConfigFilePathItCanDownloadThePackage(bool testMockBehaviorIsInSync)
        {
            var reporter = new BufferedReporter();
            var uniqueTempProjectPath = GetUniqueTempProjectPathEachTest();
            var tempProjectDirectory = uniqueTempProjectPath.GetDirectoryPath();
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            Directory.CreateDirectory(tempProjectDirectory.Value);

            /*
             * In test, we don't want NuGet to keep look up, so we point current directory to nugetconfig.
             */

            Directory.SetCurrentDirectory(nugetConfigPath.GetDirectoryPath().Value);

            IToolPackageObtainer packageObtainer;
            if (testMockBehaviorIsInSync)
            {
                packageObtainer = new ToolPackageObtainerMock();
            }
            else
            {
                packageObtainer = new ToolPackageObtainer(
                    new DirectoryPath(toolsPath),
                    new DirectoryPath("no such path"),
                    () => uniqueTempProjectPath,
                    new Lazy<string>(),
                    new ProjectRestorer(reporter));
            }

            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    targetframework: _testTargetframework);

            reporter.Lines.Should().BeEmpty();

            var executable = toolConfigurationAndExecutablePath.Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");

            File.Delete(executable.Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoPackageVersionItCanDownloadThePackage(bool testMockBehaviorIsInSync)
        {
            var reporter = new BufferedReporter();
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath, reporter, testMockBehaviorIsInSync, nugetConfigPath.Value);

            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    nugetconfig: nugetConfigPath,
                    targetframework: _testTargetframework);

            reporter.Lines.Should().BeEmpty();

            var executable = toolConfigurationAndExecutablePath.Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");

            File.Delete(executable.Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoPackageVersionAndInvokeTwiceItShouldNotThrow(bool testMockBehaviorIsInSync)
        {
            var reporter = new BufferedReporter();
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath, reporter, testMockBehaviorIsInSync, nugetConfigPath.Value);

            packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            reporter.Lines.Should().BeEmpty();

            Action secondCall = () => packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            reporter.Lines.Should().BeEmpty();

            secondCall.ShouldNotThrow();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoTargetFrameworkItCanDownloadThePackage(bool testMockBehaviorIsInSync)
        {
            var reporter = new BufferedReporter();
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            IToolPackageObtainer packageObtainer;
            if (testMockBehaviorIsInSync)
            {
                packageObtainer = new ToolPackageObtainerMock(additionalFeeds:
                    new List<MockFeed>
                    {
                        new MockFeed
                        {
                            Type = MockFeedType.ExplicitNugetConfig,
                            Uri = nugetConfigPath.Value,
                            Packages = new List<MockFeedPackage>
                            {
                                new MockFeedPackage
                                {
                                    PackageId = TestPackageId,
                                    Version = "1.0.4"
                                }
                            }
                        }
                    });
            }
            else
            {
                packageObtainer = new ToolPackageObtainer(
                    new DirectoryPath(toolsPath),
                    new DirectoryPath("no such path"),
                    GetUniqueTempProjectPathEachTest,
                    new Lazy<string>(() => BundledTargetFramework.GetTargetFrameworkMoniker()),
                    new ProjectRestorer(reporter));
            }
            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    nugetconfig: nugetConfigPath);

            reporter.Lines.Should().BeEmpty();

            var executable = toolConfigurationAndExecutablePath.Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");

            File.Delete(executable.Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNonExistentNugetConfigFileItThrows(bool testMockBehaviorIsInSync)
        {
            var reporter = new BufferedReporter();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath, reporter, testMockBehaviorIsInSync);

            var nonExistNugetConfigFile = new FilePath("NonExistent.file");
            Action a = () =>
            {
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    nugetconfig: nonExistNugetConfigFile,
                    targetframework: _testTargetframework);
            };

            a.ShouldThrow<PackageObtainException>()
                .And
                .Message.Should().Contain(string.Format(
                    CommonLocalizableStrings.NuGetConfigurationFileDoesNotExist,
                    Path.GetFullPath(nonExistNugetConfigFile.Value)));

            reporter.Lines.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenASourceItCanObtainThePackageFromThatSource(bool testMockBehaviorIsInSync)
        {
            var reporter = new BufferedReporter();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer = ConstructDefaultPackageObtainer(toolsPath, reporter);
            var toolConfigurationAndExecutableDirectory = packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                targetframework: _testTargetframework,
                source:GetTestLocalFeedPath());

            reporter.Lines.Should().BeEmpty();

            var executable = toolConfigurationAndExecutableDirectory.Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");

            File.Delete(executable.Value);
        }

        private static readonly Func<FilePath> GetUniqueTempProjectPathEachTest = () =>
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());
            var tempProjectPath =
                tempProjectDirectory.WithFile(Path.GetRandomFileName() + ".csproj");
            return tempProjectPath;
        };

        private static IToolPackageObtainer ConstructDefaultPackageObtainer(
            string toolsPath,
            IReporter reporter,
            bool testMockBehaviorIsInSync = false,
            string addNugetConfigFeedWithFilePath = null,
            string addSourceFeedWithFilePath = null)
        {
            if (testMockBehaviorIsInSync)
            {
                if (addNugetConfigFeedWithFilePath != null)
                {
                    return new ToolPackageObtainerMock(additionalFeeds:
                        new List<MockFeed>
                        {
                            new MockFeed
                            {
                                Type = MockFeedType.ExplicitNugetConfig,
                                Uri = addNugetConfigFeedWithFilePath,
                                Packages = new List<MockFeedPackage>
                                {
                                    new MockFeedPackage
                                    {
                                        PackageId = TestPackageId,
                                        Version = "1.0.4"
                                    }
                                }
                            }
                        });
                }

                if (addSourceFeedWithFilePath != null)
                {
                    return new ToolPackageObtainerMock(additionalFeeds:
                        new List<MockFeed>
                        {
                            new MockFeed
                            {
                                Type = MockFeedType.ExplicitNugetConfig,
                                Uri = addSourceFeedWithFilePath,
                                Packages = new List<MockFeedPackage>
                                {
                                    new MockFeedPackage
                                    {
                                        PackageId = TestPackageId,
                                        Version = "1.0.4"
                                    }
                                }
                            }
                        });
                }

                return new ToolPackageObtainerMock();
            }

            return new ToolPackageObtainer(
                new DirectoryPath(toolsPath),
                new DirectoryPath("no such path"),
                GetUniqueTempProjectPathEachTest,
                new Lazy<string>(),
                new ProjectRestorer(reporter));
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
        private const string TestPackageId = "global.tool.console.demo";
    }
}
