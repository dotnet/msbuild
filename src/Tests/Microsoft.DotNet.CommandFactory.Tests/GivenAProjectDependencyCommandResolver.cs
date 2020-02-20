// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.CommandFactory;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    public class GivenAProjectDependencyCommandResolver : SdkTest
    {
        private string _configuration;

        public GivenAProjectDependencyCommandResolver(ITestOutputHelper log) : base(log)
        {
            Environment.SetEnvironmentVariable(
                Constants.MSBUILD_EXE_PATH,
                Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "MSBuild.dll"));

            _configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
        }

        [Fact]
        public void ItReturnsACommandSpecWhenToolIsInAProjectRef()
        {
            var testAsset =
                _testAssetsManager.CopyTestAsset("TestAppWithProjDepTool")
                    .WithSource();

            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .Execute("--configuration", _configuration)
                .Should().Pass();

            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                Configuration = _configuration,
                ProjectDirectory = testAsset.Path,
                Framework = NuGet.Frameworks.FrameworkConstants.CommonFrameworks.NetCoreApp30
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileNameWithoutExtension(result.Path);

            commandFile.Should().Be("dotnet");

            result.Args.Should().Contain(commandResolverArguments.CommandName);
        }

        [Fact]
        public void ItPassesDepsfileArgToHostWhenReturningACommandSpecForMSBuildProject()
        {
            var testAsset =
                _testAssetsManager.CopyTestAsset("TestAppWithProjDepTool")
                    .WithSource();

            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .Execute("--configuration", _configuration)
                .Should().Pass();

            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                Configuration = _configuration,
                ProjectDirectory = testAsset.Path,
                Framework = NuGet.Frameworks.FrameworkConstants.CommonFrameworks.NetCoreApp30
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            result.Args.Should().Contain("--depsfile");
        }

        [Fact]
        public void ItReturnsNullWhenCommandNameDoesNotExistInProjectDependenciesForMSBuildProject()
        {
            var testAsset =
                _testAssetsManager.CopyTestAsset("TestAppWithProjDepTool")
                    .WithSource();

            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            new RestoreCommand(Log, testAsset.TestRoot)
                .Execute()
                .Should()
                .Pass();

            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null,
                ProjectDirectory = testAsset.Path,
                Framework = NuGet.Frameworks.FrameworkConstants.CommonFrameworks.NetCoreApp30

            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItSetsDepsfileToOutputInCommandspecForMSBuild()
        {
            var testAsset =
                _testAssetsManager.CopyTestAsset("TestAppWithProjDepTool")
                    .WithSource();

            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            new RestoreCommand(Log, testAsset.TestRoot)
                .Execute()
                .Should()
                .Pass();

            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var outputDir = Path.Combine(testAsset.Path, "out");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                Configuration = "Debug",
                ProjectDirectory = testAsset.Path,
                Framework = NuGet.Frameworks.FrameworkConstants.CommonFrameworks.NetCoreApp30,
                OutputPath = outputDir
            };

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .Execute($"-o", outputDir)
                .Should()
                .Pass();

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            var depsFilePath = Path.Combine(outputDir, "TestAppWithProjDepTool.deps.json");

            result.Should().NotBeNull();
            result.Args.Should().Contain($"--depsfile {depsFilePath}");
        }

        private ProjectDependenciesCommandResolver SetupProjectDependenciesCommandResolver(
            IEnvironmentProvider environment = null,
            IPackagedCommandSpecFactory packagedCommandSpecFactory = null)
        {
            Environment.SetEnvironmentVariable(
                Constants.MSBUILD_EXE_PATH,
                Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "MSBuild.dll"));

            environment = environment ?? new EnvironmentProvider();

            packagedCommandSpecFactory = packagedCommandSpecFactory ?? new PackagedCommandSpecFactory();

            var projectDependenciesCommandResolver = new ProjectDependenciesCommandResolver(environment, packagedCommandSpecFactory);

            return projectDependenciesCommandResolver;
        }
    }
}
