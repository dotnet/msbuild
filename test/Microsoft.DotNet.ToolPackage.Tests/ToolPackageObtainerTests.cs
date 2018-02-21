// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Install.Tool;
using Xunit;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class ToolPackageObtainerTests : TestBase
    {
        [Fact]
        public void GivenNugetConfigAndPackageNameAndVersionAndTargetFrameworkWhenCallItCanDownloadThePackage()
        {
            FilePath nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath);
            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath = packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            var executable = toolConfigurationAndExecutablePath
                .Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");
        }

        [Fact]
        public void GivenNoFeedItThrows()
        {
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            ToolPackageObtainer packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath);

            Action a = () => packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                targetframework: _testTargetframework);

            a.ShouldThrow<PackageObtainException>();
        }

        [Fact]
        public void GivenOfflineFeedWhenCallItCanDownloadThePackage()
        {
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            ToolPackageObtainer packageObtainer =
                new ToolPackageObtainer(
                    toolsPath: new DirectoryPath(toolsPath),
                    offlineFeedPath: new DirectoryPath(GetTestLocalFeedPath()),
                    getTempProjectPath: GetUniqueTempProjectPathEachTest,
                    bundledTargetFrameworkMoniker: new Lazy<string>(),
                    packageToProjectFileAdder: new PackageToProjectFileAdder(),
                    projectRestorer: new ProjectRestorer());

            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    targetframework: _testTargetframework);

            var executable = toolConfigurationAndExecutablePath
                .Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");

            executable.Value.Should().NotContain(GetTestLocalFeedPath(), "Executalbe should not be still in fallbackfolder");
            executable.Value.Should().Contain(toolsPath, "Executalbe should be copied to tools Path");
        }

        [Fact]
        public void GivenNugetConfigAndPackageNameAndVersionAndTargetFrameworkWhenCallItCreateAssetFile()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath);

            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath = packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            /*
              From mytool.dll to project.assets.json
               .dotnet/.tools/packageid/version/packageid/version/mytool.dll
                      /dependency1 package id/
                      /dependency2 package id/
                      /project.assets.json
             */
            var assetJsonPath = toolConfigurationAndExecutablePath
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
        }

        [Fact]
        public void GivenAllButNoNugetConfigFilePathItCanDownloadThePackage()
        {
            var uniqueTempProjectPath = GetUniqueTempProjectPathEachTest();
            var tempProjectDirectory = uniqueTempProjectPath.GetDirectoryPath();
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            Directory.CreateDirectory(tempProjectDirectory.Value);

            /*
             * In test, we don't want NuGet to keep look up, so we point current directory to nugetconfig.
             */

            Directory.SetCurrentDirectory(nugetConfigPath.GetDirectoryPath().Value);

            var packageObtainer =
                new ToolPackageObtainer(
                    new DirectoryPath(toolsPath),
                    new DirectoryPath("no such path"),
                    () => uniqueTempProjectPath,
                    new Lazy<string>(),
                    new PackageToProjectFileAdder(),
                    new ProjectRestorer());
            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    targetframework: _testTargetframework);

            var executable = toolConfigurationAndExecutablePath.Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");
        }

        [Fact]
        public void GivenAllButNoPackageVersionItCanDownloadThePackage()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath);
            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath = packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            var executable = toolConfigurationAndExecutablePath.Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");
        }

        [Fact]
        public void GivenAllButNoPackageVersionAndInvokeTwiceItShouldNotThrow()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath);

            packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            Action secondCall = () => packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            secondCall.ShouldNotThrow();
        }


        [Fact]
        public void GivenAllButNoTargetFrameworkItCanDownloadThePackage()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                new ToolPackageObtainer(
                    new DirectoryPath(toolsPath),
                    new DirectoryPath("no such path"),
                    GetUniqueTempProjectPathEachTest,
                    new Lazy<string>(() => BundledTargetFramework.GetTargetFrameworkMoniker()),
                    new PackageToProjectFileAdder(),
                    new ProjectRestorer());
            ToolConfigurationAndExecutablePath toolConfigurationAndExecutablePath =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    nugetconfig: nugetConfigPath);

            var executable = toolConfigurationAndExecutablePath.Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");
        }

        [Fact]
        public void GivenNonExistentNugetConfigFileItThrows()
        {
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath);
            Action a = () => packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                nugetconfig: new FilePath("NonExistent.file"),
                targetframework: _testTargetframework);

            a.ShouldThrow<PackageObtainException>()
                .And
                .Message.Should().Contain("does not exist");
        }

        [Fact]
        public void GivenASourceItCanObtainThePackageFromThatSource()
        {
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer = ConstructDefaultPackageObtainer(toolsPath);
            var toolConfigurationAndExecutableDirectory = packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                targetframework: _testTargetframework,
                source: GetTestLocalFeedPath());

            var executable = toolConfigurationAndExecutableDirectory.Executable;

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");
        }

        private static readonly Func<FilePath> GetUniqueTempProjectPathEachTest = () =>
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());
            var tempProjectPath =
                tempProjectDirectory.WithFile(Path.GetRandomFileName() + ".csproj");
            return tempProjectPath;
        };

        private static ToolPackageObtainer ConstructDefaultPackageObtainer(string toolsPath)
        {
            return new ToolPackageObtainer(
                new DirectoryPath(toolsPath),
                new DirectoryPath("no such path"),
                GetUniqueTempProjectPathEachTest,
                new Lazy<string>(),
                new PackageToProjectFileAdder(),
                new ProjectRestorer());
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
