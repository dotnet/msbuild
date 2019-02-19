// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.Tools.Tests.Utilities;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Tests
{
    public class GivenAProjectDependencyCommandResolver : TestBase
    {
        private TestAssetInstance MSBuildTestProjectInstance;

        private string _configuration;

        public GivenAProjectDependencyCommandResolver()
        {
            Environment.SetEnvironmentVariable(
                Constants.MSBUILD_EXE_PATH,
                Path.Combine(new RepoDirectoriesProvider().Stage2Sdk, "MSBuild.dll"));

            _configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
        }

        [Fact]
        public void ItReturnsACommandSpecWhenToolIsInAProjectRef()
        {
            MSBuildTestProjectInstance =
                TestAssets.Get("TestAppWithProjDepTool")
                    .CreateInstance()
                    .WithSourceFiles();
            
            new BuildCommand()
                .WithProjectDirectory(MSBuildTestProjectInstance.Root)
                .WithConfiguration(_configuration)
                .Execute()
                .Should().Pass();

            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                Configuration = _configuration,
                ProjectDirectory = MSBuildTestProjectInstance.Root.FullName,
                Framework = NuGetFrameworks.NetCoreApp30
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
            MSBuildTestProjectInstance =
                TestAssets.Get("TestAppWithProjDepTool")
                    .CreateInstance()
                    .WithSourceFiles();
            
            new BuildCommand()
                .WithProjectDirectory(MSBuildTestProjectInstance.Root)
                .WithConfiguration(_configuration)
                .Execute()
                .Should().Pass();

            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                Configuration = _configuration,
                ProjectDirectory = MSBuildTestProjectInstance.Root.FullName,
                Framework = NuGetFrameworks.NetCoreApp30
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            result.Args.Should().Contain("--depsfile");
        }

        [Fact]
        public void ItReturnsNullWhenCommandNameDoesNotExistInProjectDependenciesForMSBuildProject()
        {
            MSBuildTestProjectInstance =
                TestAssets.Get("TestAppWithProjDepTool")
                    .CreateInstance()
                    .WithSourceFiles()
                    .WithRestoreFiles();

            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null,
                ProjectDirectory = MSBuildTestProjectInstance.Root.FullName,
                Framework = NuGetFrameworks.NetCoreApp30
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItSetsDepsfileToOutputInCommandspecForMSBuild()
        {
            var testInstance = TestAssets
                    .Get("TestAppWithProjDepTool")
                    .CreateInstance()
                    .WithSourceFiles()
                    .WithRestoreFiles();

            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var outputDir = testInstance.Root.GetDirectory("out");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                Configuration = "Debug",
                ProjectDirectory = testInstance.Root.FullName,
                Framework = NuGetFrameworks.NetCoreApp30,
                OutputPath = outputDir.FullName
            };

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute($"-o {outputDir.FullName}")
                .Should()
                .Pass();

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            var depsFilePath = outputDir.GetFile("TestAppWithProjDepTool.deps.json");

            result.Should().NotBeNull();
            result.Args.Should().Contain($"--depsfile {depsFilePath.FullName}");
        }

        private ProjectDependenciesCommandResolver SetupProjectDependenciesCommandResolver(
            IEnvironmentProvider environment = null,
            IPackagedCommandSpecFactory packagedCommandSpecFactory = null)
        {
            Environment.SetEnvironmentVariable(
                Constants.MSBUILD_EXE_PATH,
                Path.Combine(new RepoDirectoriesProvider().Stage2Sdk, "MSBuild.dll"));

            CommandContext.SetVerbose(true);

            environment = environment ?? new EnvironmentProvider();

            packagedCommandSpecFactory = packagedCommandSpecFactory ?? new PackagedCommandSpecFactory();

            var projectDependenciesCommandResolver = new ProjectDependenciesCommandResolver(environment, packagedCommandSpecFactory);

            return projectDependenciesCommandResolver;
        }
    }
}
