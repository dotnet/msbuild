// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAProjectDependenciesCommandResolver
    {

        private static readonly string s_liveProjectDirectory =
            Path.Combine(AppContext.BaseDirectory, "TestAssets/TestProjects/AppWithDirectDependency");

        [Fact]
        public void It_returns_null_when_CommandName_is_null()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
                CommandArguments = new string[] { "" },
                ProjectDirectory = "/some/directory",
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_ProjectDirectory_is_null()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "command",
                CommandArguments = new string[] { "" },
                ProjectDirectory = null,
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_Framework_is_null()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "command",
                CommandArguments = new string[] { "" },
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = "Debug",
                Framework = null
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_Configuration_is_null()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "command",
                CommandArguments = new string[] { "" },
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = null,
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_CommandName_does_not_exist_in_ProjectDependencies()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_Dotnet_as_FileName_and_CommandName_in_Args_when_CommandName_exists_in_ProjectDependencies()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-hello",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileNameWithoutExtension(result.Path);

            commandFile.Should().Be("dotnet");

            result.Args.Should().Contain(commandResolverArguments.CommandName);
        }

        [Fact]
        public void It_escapes_CommandArguments_when_returning_a_CommandSpec()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-hello",
                CommandArguments = new[] { "arg with space" },
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Contain("\"arg with space\"");
        }

        [Fact]
        public void It_passes_depsfile_arg_to_host_when_returning_a_commandspec()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-hello",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Contain("--depsfile");
        }

        [Fact]
        public void It_sets_depsfile_in_output_path_in_commandspec()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();
            var outputDir = Path.Combine(AppContext.BaseDirectory, "outdir");

            var commandResolverArguments = new CommandResolverArguments
            {
                CommandName = "dotnet-hello",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10,
                OutputPath = outputDir
            };

            var buildCommand = new BuildCommand(
                Path.Combine(s_liveProjectDirectory, "project.json"),
                output: outputDir,
                framework: FrameworkConstants.CommonFrameworks.NetCoreApp10.ToString())
                .Execute().Should().Pass();

            var projectContext = ProjectContext.Create(
                s_liveProjectDirectory,
                FrameworkConstants.CommonFrameworks.NetCoreApp10,
                RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());

            var depsFilePath =
                projectContext.GetOutputPaths("Debug", outputPath: outputDir).RuntimeFiles.DepsJson;

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Contain($"--depsfile {depsFilePath}");
        }

        [Fact]
        public void It_sets_depsfile_in_build_base_path_in_commandspec()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();
            var buildBasePath = Path.Combine(AppContext.BaseDirectory, "basedir");

            var commandResolverArguments = new CommandResolverArguments
            {
                CommandName = "dotnet-hello",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10,
                BuildBasePath = buildBasePath
            };

            var buildCommand = new BuildCommand(
                Path.Combine(s_liveProjectDirectory, "project.json"),
                buildBasePath: buildBasePath,
                framework: FrameworkConstants.CommonFrameworks.NetCoreApp10.ToString())
                .Execute().Should().Pass();

            var projectContext = ProjectContext.Create(
                s_liveProjectDirectory,
                FrameworkConstants.CommonFrameworks.NetCoreApp10,
                RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());

            var depsFilePath =
                projectContext.GetOutputPaths("Debug", buildBasePath).RuntimeFiles.DepsJson;

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Contain($"--depsfile {depsFilePath}");
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_CommandName_in_Args_when_returning_a_CommandSpec_and_CommandArguments_are_null()
        {
            var projectDependenciesCommandResolver = SetupProjectDependenciesCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-hello",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory,
                Configuration = "Debug",
                Framework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var result = projectDependenciesCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            result.Args.Should().Contain("dotnet-hello");
        }

        private ProjectDependenciesCommandResolver SetupProjectDependenciesCommandResolver(
            IEnvironmentProvider environment = null,
            IPackagedCommandSpecFactory packagedCommandSpecFactory = null)
        {
            environment = environment ?? new EnvironmentProvider();
            packagedCommandSpecFactory = packagedCommandSpecFactory ?? new PackagedCommandSpecFactory();

            var projectDependenciesCommandResolver = new ProjectDependenciesCommandResolver(environment, packagedCommandSpecFactory);

            return projectDependenciesCommandResolver;
        }
    }
}
