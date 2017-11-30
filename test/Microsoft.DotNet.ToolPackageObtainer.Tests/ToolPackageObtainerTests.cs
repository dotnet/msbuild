// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Cli;
using Xunit;

namespace Microsoft.DotNet.ToolPackageObtainer.Tests
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
            ToolConfigurationAndExecutableDirectory toolConfigurationAndExecutableDirectory = packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            var executable = toolConfigurationAndExecutableDirectory
                .ExecutableDirectory
                .WithFile(
                    toolConfigurationAndExecutableDirectory
                        .Configuration
                        .ToolAssemblyEntryPoint);

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");
        }

        [Fact]
        public void GivenNugetConfigAndPackageNameAndVersionAndTargetFrameworkWhenCallItCreateAssetFile()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                ConstructDefaultPackageObtainer(toolsPath);
            ToolConfigurationAndExecutableDirectory toolConfigurationAndExecutableDirectory =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    nugetconfig: nugetConfigPath,
                    targetframework: _testTargetframework);

            var assetJsonPath = toolConfigurationAndExecutableDirectory
                .ExecutableDirectory
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
             * No nuget config means you don't need nuget config passed in during call
             * NuGet needs a way to find the package, in production, it will keep look up folders for Nuget.Config 
             * and use the feed there.
             * In test, we don't want NuGet to keep look up, so we just copy paste beside the project.
             */
            File.Copy(nugetConfigPath.Value,
                tempProjectDirectory.WithFile("nuget.config").Value);

            var packageObtainer =
                new ToolPackageObtainer(
                    new DirectoryPath(toolsPath),
                    () => uniqueTempProjectPath,
                    new Lazy<string>(),
                    new PackageToProjectFileAdder(),
                    new ProjectRestorer());
            ToolConfigurationAndExecutableDirectory toolConfigurationAndExecutableDirectory = packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                packageVersion: TestPackageVersion,
                targetframework: _testTargetframework);

            var executable = toolConfigurationAndExecutableDirectory
                .ExecutableDirectory
                .WithFile(
                    toolConfigurationAndExecutableDirectory
                        .Configuration
                        .ToolAssemblyEntryPoint);

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
            ToolConfigurationAndExecutableDirectory toolConfigurationAndExecutableDirectory = packageObtainer.ObtainAndReturnExecutablePath(
                packageId: TestPackageId,
                nugetconfig: nugetConfigPath,
                targetframework: _testTargetframework);

            var executable = toolConfigurationAndExecutableDirectory
                .ExecutableDirectory
                .WithFile(
                    toolConfigurationAndExecutableDirectory
                        .Configuration
                        .ToolAssemblyEntryPoint);

            File.Exists(executable.Value)
                .Should()
                .BeTrue(executable + " should have the executable");
        }

        [Fact]
        public void GivenAllButNoTargetFrameworkItCanDownloadThePackage()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName());

            var packageObtainer =
                new ToolPackageObtainer(
                    new DirectoryPath(toolsPath),
                    GetUniqueTempProjectPathEachTest,
                    new Lazy<string>(() => BundledTargetFramework.GetTargetFrameworkMoniker()),
                    new PackageToProjectFileAdder(),
                    new ProjectRestorer());
            ToolConfigurationAndExecutableDirectory toolConfigurationAndExecutableDirectory =
                packageObtainer.ObtainAndReturnExecutablePath(
                    packageId: TestPackageId,
                    packageVersion: TestPackageVersion,
                    nugetconfig: nugetConfigPath);

            var executable = toolConfigurationAndExecutableDirectory
                .ExecutableDirectory
                .WithFile(
                    toolConfigurationAndExecutableDirectory
                        .Configuration
                        .ToolAssemblyEntryPoint);

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
                GetUniqueTempProjectPathEachTest,
                new Lazy<string>(),
                new PackageToProjectFileAdder(),
                new ProjectRestorer());
        }

        private static FilePath WriteNugetConfigFileToPointToTheFeed()
        {
            var nugetConfigName = Path.GetRandomFileName() + ".config";
            var executeDirectory =
                Path.GetDirectoryName(
                    System.Reflection
                        .Assembly
                        .GetExecutingAssembly()
                        .Location);
            NuGetConfig.Write(
                directory: executeDirectory,
                configname: nugetConfigName,
                localFeedPath: Path.Combine(executeDirectory, "TestAssetLocalNugetFeed"));
            return new FilePath(Path.GetFullPath(nugetConfigName));
        }

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private const string TestPackageId = "global.tool.console.demo";
    }
}
