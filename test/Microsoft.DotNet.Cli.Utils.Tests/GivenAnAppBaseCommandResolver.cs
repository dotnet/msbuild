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
    public class GivenAnAppBaseCommandResolver
    {
        [Fact]
        public void It_returns_null_when_CommandName_is_null()
        {
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
                CommandArguments = null
            };

            var result = appBaseCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_CommandName_does_not_exist_applocal()
        {
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null
            };

            var result = appBaseCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_CommandName_as_FileName_when_CommandName_exists_applocal()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(environment);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(AppContext.BaseDirectory, "appbasetestcommand1", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "appbasetestcommand1",
                CommandArguments = null
            };

            var result = appBaseCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileNameWithoutExtension(result.Path);

            commandFile.Should().Be("appbasetestcommand1");
        }

        [Fact]
        public void It_returns_null_when_CommandName_exists_applocal_in_a_subdirectory()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(environment);

            var testDir = Path.Combine(AppContext.BaseDirectory, "appbasetestsubdir");
            CommandResolverTestUtils.CreateNonRunnableTestCommand(testDir, "appbasetestsubdircommand", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "appbasetestsubdircommand",
                CommandArguments = null
            };

            var result = appBaseCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_escapes_CommandArguments_when_returning_a_CommandSpec()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(environment);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(AppContext.BaseDirectory, "appbasetestcommand1", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "appbasetestcommand1",
                CommandArguments = new [] { "arg with space"}
            };

            var result = appBaseCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Be("\"arg with space\"");
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_Args_as_stringEmpty_when_returning_a_CommandSpec_and_CommandArguments_are_null()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(environment);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(AppContext.BaseDirectory, "appbasetestcommand1", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "appbasetestcommand1",
                CommandArguments = null
            };

            var result = appBaseCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Be(string.Empty);
        }

        [Fact]
        public void It_prefers_EXE_over_CMD_when_two_command_candidates_exist_and_using_WindowsExePreferredCommandSpecFactory()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();

            var appBaseCommandResolver = new AppBaseCommandResolver(environment, platformCommandSpecFactory);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(AppContext.BaseDirectory, "appbasetestcommand1", ".exe");
            CommandResolverTestUtils.CreateNonRunnableTestCommand(AppContext.BaseDirectory, "appbasetestcommand1", ".cmd");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "appbasetestcommand1",
                CommandArguments = null
            };

            var result = appBaseCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileName(result.Path);
            commandFile.Should().Be("appbasetestcommand1.exe");
        }

        private AppBaseCommandResolver SetupPlatformAppBaseCommandResolver(IEnvironmentProvider environment = null)
        {
            environment = environment ?? new EnvironmentProvider();

            var platformCommandSpecFactory = default(IPlatformCommandSpecFactory);

            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
            {
                platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
            }
            else
            {
                platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
            }

            var appBaseCommandResolver = new AppBaseCommandResolver(environment, platformCommandSpecFactory);

            return appBaseCommandResolver;
        }
    }
}
