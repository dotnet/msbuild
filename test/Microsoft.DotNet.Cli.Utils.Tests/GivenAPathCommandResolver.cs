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
    public class GivenAPathCommandResolver
    {
        private static readonly string s_testDirectory = Path.Combine(AppContext.BaseDirectory, "pathTestDirectory");

        [Fact]
        public void It_returns_null_when_CommandName_is_null()
        {
            var pathCommandResolver = SetupPlatformPathCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
                CommandArguments = null
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_CommandName_does_not_exist_in_PATH()
        {
            var emptyPathEnvironmentMock = new Mock<IEnvironmentProvider>();
            emptyPathEnvironmentMock.Setup(e => e
                .GetCommandPath(It.IsAny<string>(), It.IsAny<string[]>()))
                .Returns((string)null);

            var pathCommandResolver = SetupPlatformPathCommandResolver(emptyPathEnvironmentMock.Object);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_CommandName_as_FileName_when_CommandName_exists_in_PATH()
        {
            var testCommandPath = CommandResolverTestUtils.CreateNonRunnableTestCommand(
                s_testDirectory, 
                "pathtestcommand1", 
                ".exe");

            var staticPathEnvironmentMock = new Mock<IEnvironmentProvider>();
            staticPathEnvironmentMock.Setup(e => e
                .GetCommandPath(It.IsAny<string>(), It.IsAny<string[]>()))
                .Returns(testCommandPath); 

            var pathCommandResolver = SetupPlatformPathCommandResolver(staticPathEnvironmentMock.Object, forceGeneric: true);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = Path.GetFileNameWithoutExtension(testCommandPath),
                CommandArguments = null
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileNameWithoutExtension(result.Path);

            commandFile.Should().Be(Path.GetFileNameWithoutExtension(testCommandPath));
        }

        [Fact]
        public void It_escapes_CommandArguments_when_returning_a_CommandSpec()
        {
            var testCommandPath = CommandResolverTestUtils.CreateNonRunnableTestCommand(
                s_testDirectory, 
                "pathtestcommand1", 
                ".exe");

            var staticPathEnvironmentMock = new Mock<IEnvironmentProvider>();
            staticPathEnvironmentMock.Setup(e => e
                .GetCommandPath(It.IsAny<string>(), It.IsAny<string[]>()))
                .Returns(testCommandPath); 

            var pathCommandResolver = SetupPlatformPathCommandResolver(staticPathEnvironmentMock.Object, forceGeneric: true);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = Path.GetFileNameWithoutExtension(testCommandPath),
                CommandArguments = new [] {"arg with space"}
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Be("\"arg with space\"");
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_Args_as_stringEmpty_when_returning_a_CommandSpec_and_CommandArguments_are_null()
        {
            var testCommandPath = CommandResolverTestUtils.CreateNonRunnableTestCommand(
                s_testDirectory, 
                "pathtestcommand1", 
                ".exe");

            var staticPathEnvironmentMock = new Mock<IEnvironmentProvider>();
            staticPathEnvironmentMock.Setup(e => e
                .GetCommandPath(It.IsAny<string>(), It.IsAny<string[]>()))
                .Returns(testCommandPath); 

            var pathCommandResolver = SetupPlatformPathCommandResolver(staticPathEnvironmentMock.Object, forceGeneric: true);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = Path.GetFileNameWithoutExtension(testCommandPath),
                CommandArguments = null
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Be(string.Empty);
        }

        [Fact]
        public void It_prefers_EXE_over_CMD_when_two_command_candidates_exist_and_using_WindowsExePreferredCommandSpecFactory()
        {
            var environment = new EnvironmentProvider(new [] {".exe", ".cmd"}, new[] { s_testDirectory });
            var platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();

            var pathCommandResolver = new PathCommandResolver(environment, platformCommandSpecFactory);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testDirectory, "extensionPreferenceCommand", ".exe");
            CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testDirectory, "extensionPreferenceCommand", ".cmd");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "extensionPreferenceCommand",
                CommandArguments = null
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileName(result.Path);
            commandFile.Should().Be("extensionPreferenceCommand.exe");
        }

        [Fact]
        public void It_wraps_command_with_CMD_EXE_when_command_has_CMD_Extension_and_using_WindowsExePreferredCommandSpecFactory()
        {
            var environment = new EnvironmentProvider(new [] {".cmd"}, new[] { s_testDirectory });
            var platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();

            var pathCommandResolver = new PathCommandResolver(environment, platformCommandSpecFactory);

            var testCommandPath = 
                CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testDirectory, "cmdWrapCommand", ".cmd");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "cmdWrapCommand",
                CommandArguments = null
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileName(result.Path);
            commandFile.Should().Be("cmd.exe");

            result.Args.Should().Contain(testCommandPath);
        }

        private PathCommandResolver SetupPlatformPathCommandResolver(
            IEnvironmentProvider environment = null, 
            bool forceGeneric = false)
        {
            environment = environment ?? new EnvironmentProvider();

            IPlatformCommandSpecFactory platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();

            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows
                && !forceGeneric)
            {
                platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
            }

            var pathCommandResolver = new PathCommandResolver(environment, platformCommandSpecFactory);

            return pathCommandResolver;
        }
    }
}
