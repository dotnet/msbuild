// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using Xunit;
using Moq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using System.Threading;
using FluentAssertions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAProjectToolsCommandResolver
    {

        private static readonly string s_liveProjectDirectory = 
            Path.Combine(AppContext.BaseDirectory, "TestAssets/TestProjects/AppWithToolDependency");

        [Fact]
        public void It_returns_null_when_CommandName_is_null()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
                CommandArguments = new string[] {""},
                ProjectDirectory = "/some/directory"
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_ProjectDirectory_is_null()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "command",
                CommandArguments = new string[] {""},
                ProjectDirectory = null
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_CommandName_does_not_exist_in_ProjectTools()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_CoreHost_as_FileName_and_CommandName_in_Args_when_CommandName_exists_in_ProjectTools()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-hello",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileNameWithoutExtension(result.Path);

            commandFile.Should().Be("corehost");

            result.Args.Should().Contain(commandResolverArguments.CommandName);
        }

        [Fact]
        public void It_escapes_CommandArguments_when_returning_a_CommandSpec()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-hello",
                CommandArguments = new [] { "arg with space"},
                ProjectDirectory = s_liveProjectDirectory
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Contain("\"arg with space\"");
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_Args_as_CommandPath_when_returning_a_CommandSpec_and_CommandArguments_are_null()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-hello",
                CommandArguments = null,
                ProjectDirectory = s_liveProjectDirectory
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            
            var commandPath = result.Args.Trim('"');
            commandPath.Should().Contain("dotnet-hello");

            File.Exists(commandPath).Should().BeTrue();
        }

        private ProjectToolsCommandResolver SetupProjectToolsCommandResolver(
            IPackagedCommandSpecFactory packagedCommandSpecFactory = null)
        {
            packagedCommandSpecFactory = packagedCommandSpecFactory ?? new PackagedCommandSpecFactory();

            var projectToolsCommandResolver = new ProjectToolsCommandResolver(packagedCommandSpecFactory);

            return projectToolsCommandResolver;
        }
    }
}
