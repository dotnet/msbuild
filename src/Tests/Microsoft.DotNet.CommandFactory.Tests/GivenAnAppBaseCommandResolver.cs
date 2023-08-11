// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Tests
{
    public class GivenAnAppBaseCommandResolver
    {
        [Fact]
        public void It_returns_null_when_CommandName_is_null()
        {
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(forceGeneric: true);

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
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(forceGeneric: true);

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
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(environment, forceGeneric: true);

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
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(environment, forceGeneric: true);

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
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(environment, forceGeneric: true);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(AppContext.BaseDirectory, "appbasetestcommand1", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "appbasetestcommand1",
                CommandArguments = new[] { "arg with space" }
            };

            var result = appBaseCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Be("\"arg with space\"");
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_Args_as_stringEmpty_when_returning_a_CommandSpec_and_CommandArguments_are_null()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var appBaseCommandResolver = SetupPlatformAppBaseCommandResolver(environment, forceGeneric: true);

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

        [WindowsOnlyFact]
        public void It_wraps_command_with_CMD_EXE_when_command_has_CMD_Extension_and_using_WindowsExePreferredCommandSpecFactory()
        {
            var environment = new EnvironmentProvider(new[] { ".cmd" });
            var platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();

            var pathCommandResolver = new PathCommandResolver(environment, platformCommandSpecFactory);

            var testCommandPath =
                CommandResolverTestUtils.CreateNonRunnableTestCommand(AppContext.BaseDirectory, "someWrapCommand", ".cmd");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "someWrapCommand",
                CommandArguments = null
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileName(result.Path);
            commandFile.Should().EndWith("cmd.exe");

            result.Args.Should().Contain(testCommandPath);
        }

        private AppBaseCommandResolver SetupPlatformAppBaseCommandResolver(
            IEnvironmentProvider environment = null,
            bool forceGeneric = false)
        {
            environment = environment ?? new EnvironmentProvider();

            IPlatformCommandSpecFactory platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && !forceGeneric)
            {
                platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
            }

            var appBaseCommandResolver = new AppBaseCommandResolver(environment, platformCommandSpecFactory);

            return appBaseCommandResolver;
        }
    }
}
