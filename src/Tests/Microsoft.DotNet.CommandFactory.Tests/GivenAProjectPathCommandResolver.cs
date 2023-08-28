// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Tests
{
    public class GivenAProjectPathCommandResolver
    {
        private static readonly string s_testProjectDirectory = Path.Combine(AppContext.BaseDirectory, "testprojectdirectory");

        [Fact]
        public void It_returns_null_when_CommandName_is_null()
        {
            var projectPathCommandResolver = SetupPlatformProjectPathCommandResolver(forceGeneric: true);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
                CommandArguments = new string[] { "" },
                ProjectDirectory = "/some/directory"
            };

            var result = projectPathCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_ProjectDirectory_is_null()
        {
            var projectPathCommandResolver = SetupPlatformProjectPathCommandResolver(forceGeneric: true);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "command",
                CommandArguments = new string[] { "" },
                ProjectDirectory = null
            };

            var result = projectPathCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_CommandName_does_not_exist_in_ProjectDirectory()
        {
            var projectPathCommandResolver = SetupPlatformProjectPathCommandResolver(forceGeneric: true);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null,
                ProjectDirectory = s_testProjectDirectory
            };

            var result = projectPathCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_CommandName_exists_in_a_subdirectory_of_ProjectDirectory()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var projectPathCommandResolver = SetupPlatformProjectPathCommandResolver(environment, forceGeneric: true);

            var testDir = Path.Combine(s_testProjectDirectory, "projectpathtestsubdir");
            CommandResolverTestUtils.CreateNonRunnableTestCommand(testDir, "projectpathtestsubdircommand", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "projectpathtestsubdircommand",
                CommandArguments = null,
                ProjectDirectory = s_testProjectDirectory
            };

            var result = projectPathCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_CommandName_as_FileName_when_CommandName_exists_in_ProjectDirectory()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var projectPathCommandResolver = SetupPlatformProjectPathCommandResolver(environment, forceGeneric: true);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testProjectDirectory, "projectpathtestcommand1", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "projectpathtestcommand1",
                CommandArguments = null,
                ProjectDirectory = s_testProjectDirectory
            };

            var result = projectPathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileNameWithoutExtension(result.Path);

            commandFile.Should().Be("projectpathtestcommand1");
        }

        [Fact]
        public void It_escapes_CommandArguments_when_returning_a_CommandSpec()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var projectPathCommandResolver = SetupPlatformProjectPathCommandResolver(environment, forceGeneric: true);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testProjectDirectory, "projectpathtestcommand1", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "projectpathtestcommand1",
                CommandArguments = new[] { "arg with space" },
                ProjectDirectory = s_testProjectDirectory
            };

            var result = projectPathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Be("\"arg with space\"");
        }

        [Fact]
        public void It_resolves_commands_with_extensions_defined_in_InferredExtensions()
        {
            var extensions = new string[] { ".sh", ".cmd", ".foo", ".exe" };
            var projectPathCommandResolver = SetupPlatformProjectPathCommandResolver(forceGeneric: true);

            foreach (var extension in extensions)
            {
                var extensionTestDir = Path.Combine(s_testProjectDirectory, "testext" + extension);

                CommandResolverTestUtils.CreateNonRunnableTestCommand(extensionTestDir, "projectpathexttest", extension);

                var commandResolverArguments = new CommandResolverArguments()
                {
                    CommandName = "projectpathexttest",
                    CommandArguments = null,
                    ProjectDirectory = extensionTestDir,
                    InferredExtensions = extensions
                };

                var result = projectPathCommandResolver.Resolve(commandResolverArguments);

                result.Should().NotBeNull();

                var commandFileName = Path.GetFileName(result.Path);
                commandFileName.Should().Be("projectpathexttest" + extension);
            }
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_Args_as_stringEmpty_when_returning_a_CommandSpec_and_CommandArguments_are_null()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var projectPathCommandResolver = SetupPlatformProjectPathCommandResolver(environment, forceGeneric: true);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testProjectDirectory, "projectpathtestcommand1", ".exe");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "projectpathtestcommand1",
                CommandArguments = null,
                ProjectDirectory = s_testProjectDirectory
            };

            var result = projectPathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Be(string.Empty);
        }

        [Fact]
        public void It_prefers_EXE_over_CMD_when_two_command_candidates_exist_and_using_WindowsExePreferredCommandSpecFactory()
        {
            var environment = CommandResolverTestUtils.SetupEnvironmentProviderWhichFindsExtensions(".exe");
            var platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();

            var projectPathCommandResolver = new ProjectPathCommandResolver(environment, platformCommandSpecFactory);

            CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testProjectDirectory, "projectpathtestcommand1", ".exe");
            CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testProjectDirectory, "projectpathtestcommand1", ".cmd");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "projectpathtestcommand1",
                CommandArguments = null,
                ProjectDirectory = s_testProjectDirectory
            };

            var result = projectPathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileName(result.Path);
            commandFile.Should().Be("projectpathtestcommand1.exe");
        }

        [WindowsOnlyFact]
        public void It_wraps_command_with_CMD_EXE_when_command_has_CMD_Extension_and_using_WindowsExePreferredCommandSpecFactory()
        {
            var environment = new EnvironmentProvider(new[] { ".cmd" });
            var platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();

            var pathCommandResolver = new ProjectPathCommandResolver(environment, platformCommandSpecFactory);

            var testCommandPath =
                CommandResolverTestUtils.CreateNonRunnableTestCommand(s_testProjectDirectory, "cmdWrapCommand", ".cmd");

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "cmdWrapCommand",
                CommandArguments = null,
                ProjectDirectory = s_testProjectDirectory
            };

            var result = pathCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileName(result.Path);
            commandFile.Should().EndWith("cmd.exe");

            result.Args.Should().Contain(testCommandPath);
        }

        private ProjectPathCommandResolver SetupPlatformProjectPathCommandResolver(
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

            var projectPathCommandResolver = new ProjectPathCommandResolver(environment, platformCommandSpecFactory);

            return projectPathCommandResolver;
        }
    }
}
