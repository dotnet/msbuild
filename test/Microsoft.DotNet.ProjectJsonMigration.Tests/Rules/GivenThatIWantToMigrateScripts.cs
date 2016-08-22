// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.ProjectJsonMigration;
using System;
using System.IO;
using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateScripts : TestBase
    {
        [Theory]
        [InlineData("compile:FullTargetFramework", "$(TargetFrameworkIdentifier)=$(TargetFrameworkVersion)")]
        [InlineData("compile:Configuration", "$(Configuration)")]
        [InlineData("compile:OutputFile", "$(TargetPath)")]
        [InlineData("compile:OutputDir", "$(TargetDir)")]
        [InlineData("publish:ProjectPath", "$(MSBuildThisFileDirectory)")]
        [InlineData("publish:Configuration", "$(Configuration)")]
        [InlineData("publish:OutputPath", "$(TargetDir)")]
        [InlineData("publish:FullTargetFramework", "$(TargetFrameworkIdentifier)=$(TargetFrameworkVersion)")]
        [InlineData("project:Version", "$(Version)")]
        [InlineData("project:Name", "$(MSBuildThisFileName)")]
        [InlineData("project:Directory", "$(MSBuildProjectDirectory)")]
        public void Formatting_script_commands_replaces_variables_with_the_right_msbuild_properties(
            string variable, 
            string msbuildReplacement)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            scriptMigrationRule.ReplaceScriptVariables($"%{variable}%").Should().Be(msbuildReplacement);
        }

        [Theory]
        [InlineData("compile:TargetFramework")]
        [InlineData("compile:ResponseFile")]
        [InlineData("compile:CompilerExitCode")]
        [InlineData("compile:RuntimeOutputDir")]
        [InlineData("compile:RuntimeIdentifier")]
        [InlineData("publish:TargetFramework")]
        [InlineData("publish:Runtime")]
        public void Formatting_script_commands_throws_when_variable_is_unsupported(string unsupportedVariable)
        {
            var scriptMigrationRule = new MigrateScriptsRule();

            Action formatScriptAction = () => scriptMigrationRule.ReplaceScriptVariables($"%{unsupportedVariable}%");
            formatScriptAction.ShouldThrow<Exception>()
                .Where(exc => exc.Message.Contains("is currently an unsupported script variable for project migration"));
        }

        [Theory]
        [InlineData("precompile", "Build")]
        [InlineData("prepublish", "Publish")]
        public void Migrating_pre_scripts_populates_BeforeTargets_with_appropriate_target(string scriptName, string targetName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();
            var commands = new string[] { "fakecommand" };

            var target = scriptMigrationRule.MigrateScriptSet(mockProj, mockProj.AddPropertyGroup(),  commands, scriptName);

            target.BeforeTargets.Should().Be(targetName);
        }

        [Theory]
        [InlineData("postcompile", "Build")]
        [InlineData("postpublish", "Publish")]
        public void Migrating_post_scripts_populates_AfterTargets_with_appropriate_target(string scriptName, string targetName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();
            var commands = new string[] { "fakecommand" };

            var target = scriptMigrationRule.MigrateScriptSet(mockProj, mockProj.AddPropertyGroup(), commands, scriptName);

            target.AfterTargets.Should().Be(targetName);
        }

        [Theory]
        [InlineData("precompile")]
        [InlineData("postcompile")]
        [InlineData("prepublish")]
        [InlineData("postpublish")]
        public void Migrating_scripts_with_multiple_commands_creates_Exec_task_for_each(string scriptName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new string[] { "fakecommand1", "fakecommand2", "mockcommand3" };
            var commandsInTask = commands.ToDictionary(c => c, c => false);

            var target = scriptMigrationRule.MigrateScriptSet(mockProj, mockProj.AddPropertyGroup(), commands, scriptName);

            foreach (var task in target.Tasks)
            {
                var taskCommand = task.GetParameter("Command");
                var originalCommandCandidates = commands.Where(c => taskCommand.Contains(c));
                originalCommandCandidates.Count().Should().Be(1);

                var command = originalCommandCandidates.First();
                commandsInTask[command].Should().Be(false, "Expected to find each element from commands Array once");

                commandsInTask[command] = true;
            }

            commandsInTask.All(commandInTask => commandInTask.Value)
                .Should()
                .BeTrue("Expected each element from commands array to be found in a task");
        }

        [Theory]
        [InlineData("precompile")]
        [InlineData("postcompile")]
        [InlineData("prepublish")]
        [InlineData("postpublish")]
        public void Migrated_ScriptSet_has_Exec_and_replaces_variables(string scriptName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new string[] { "compile:FullTargetFramework", "compile:Configuration"};

            var target = scriptMigrationRule.MigrateScriptSet(mockProj, mockProj.AddPropertyGroup(), commands, scriptName);
            target.Tasks.Count().Should().Be(commands.Length);

            foreach (var task in target.Tasks)
            {
                var taskCommand = task.GetParameter("Command");
                var commandIndex = Array.IndexOf(commands, taskCommand);

                commandIndex.Should().Be(-1, "Expected command array elements to be replaced by appropriate msbuild properties");
            }
        }

        [Theory]
        [InlineData("precompile")]
        [InlineData("postcompile")]
        [InlineData("prepublish")]
        [InlineData("postpublish")]
        public void Migrated_ScriptSet_has_two_MigratedScriptExtensionProperties_for_each_script(string scriptName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new string[] {"compile:FullTargetFramework", "compile:Configuration"};
            var propertyGroup = mockProj.AddPropertyGroup();
            var target = scriptMigrationRule.MigrateScriptSet(mockProj, propertyGroup, commands,
                scriptName);

            Console.WriteLine(string.Join(";", propertyGroup.Properties.Select(n => n.Name)));
            propertyGroup.Properties.Count().Should().Be(commands.Length * 2);

            var count = 0;
            foreach (var command in commands)
            {
                count += 1;
                var scriptExtensionProperties =
                    propertyGroup.Properties.Where(p => p.Name.Contains($"MigratedScriptExtension_{count}")).ToArray();

                scriptExtensionProperties.All(p => p.Value == ".sh" || p.Value == ".cmd").Should().BeTrue();
                scriptExtensionProperties.Count().Should().Be(2);
            }
        }

        [Theory]
        [InlineData("echo", ".\\echo$(MigratedScriptExtension_1)")]
        [InlineData("echo hello world", ".\\echo$(MigratedScriptExtension_1) hello world")]
        [InlineData("\"echo\"", ".\\\"echo$(MigratedScriptExtension_1)\"")]
        [InlineData("\"echo space\"", ".\\\"echo space$(MigratedScriptExtension_1)\"")]
        [InlineData("\"echo space\" other args", ".\\\"echo space$(MigratedScriptExtension_1)\" other args")]
        [InlineData("\"echo space\" \"other space\"", ".\\\"echo space$(MigratedScriptExtension_1)\" \"other space\"")]
        public void Migrated_ScriptSet_has_ScriptExtension_added_to_script_command(string scriptCommandline, string expectedOutputCommand)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var formattedCommand = scriptMigrationRule.AddScriptExtensionPropertyToCommandLine(scriptCommandline,
                "MigratedScriptExtension_1");

            formattedCommand.Should().Be(expectedOutputCommand);
        }

        [Theory]
        [InlineData("echo", @".\echo")]
        [InlineData("/usr/echo", "/usr/echo")]
        [InlineData(@"C:\usr\echo", @"C:\usr\echo")]
        [InlineData("\"echo\"", @".\""echo")]
        [InlineData("\"/usr/echo\"", @"""/usr/echo")]
        [InlineData(@"""C:\usr\echo", @"""C:\usr\echo")]
        public void Migrated_ScriptSet_has_dotSlash_prepended_when_command_is_not_rooted(string scriptCommandline,
            string expectedOutputCommandPrefix)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var formattedCommand = scriptMigrationRule.FormatScriptCommand(scriptCommandline,
                "MigratedScriptExtension_1");

            formattedCommand.Should().StartWith(expectedOutputCommandPrefix);
        }
    }
}
