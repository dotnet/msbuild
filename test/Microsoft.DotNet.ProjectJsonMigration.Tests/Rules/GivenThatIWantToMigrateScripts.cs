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
using Microsoft.DotNet.ProjectJsonMigration.Rules;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateScripts : TestBase
    {
        private const bool IsMultiTFM = true;

        [Theory]
        [InlineData("compile:TargetFramework", "$(TargetFramework)")]
        [InlineData("publish:TargetFramework", "$(TargetFramework)")]
        [InlineData("compile:FullTargetFramework", "$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)")]
        [InlineData("compile:Configuration", "$(Configuration)")]
        [InlineData("compile:OutputFile", "$(TargetPath)")]
        [InlineData("compile:OutputDir", "$(TargetDir)")]
        [InlineData("publish:ProjectPath", "$(MSBuildThisFileDirectory)")]
        [InlineData("publish:Configuration", "$(Configuration)")]
        [InlineData("publish:OutputPath", "$(TargetDir)")]
        [InlineData("publish:FullTargetFramework", "$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)")]
        [InlineData("project:Version", "$(Version)")]
        [InlineData("project:Name", "$(AssemblyName)")]
        [InlineData("project:Directory", "$(MSBuildProjectDirectory)")]
        [InlineData("publish:Runtime", "$(RuntimeIdentifier)")]
        public void FormattingScriptCommandsReplacesVariablesWithTheRightMSBuildProperties(
            string variable, 
            string msbuildReplacement)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            scriptMigrationRule.ReplaceScriptVariables($"%{variable}%").Should().Be(msbuildReplacement);
        }

        [Theory]
        [InlineData("compile:ResponseFile")]
        [InlineData("compile:CompilerExitCode")]
        [InlineData("compile:RuntimeOutputDir")]
        [InlineData("compile:RuntimeIdentifier")]
        public void FormattingScriptCommandsThrowsWhenVariableIsUnsupported(string unsupportedVariable)
        {
            var scriptMigrationRule = new MigrateScriptsRule();

            Action formatScriptAction = () => scriptMigrationRule.ReplaceScriptVariables($"%{unsupportedVariable}%");
            formatScriptAction.ShouldThrow<Exception>()
                .Where(exc => exc.Message.Contains("is currently an unsupported script variable for project migration"));
        }

        [Theory]
        [InlineData("precompile", "BeforeBuild")]
        [InlineData("prepublish", "PrepareForPublish")]
        public void MigratingPreScriptsPopulatesBeforeTargetsWithAppropriateTarget(
            string scriptName,
            string targetName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();
            var commands = new string[] { "fakecommand" };

            var target = scriptMigrationRule.MigrateScriptSet(
                mockProj,
                commands,
                scriptName,
                IsMultiTFM);

            target.BeforeTargets.Should().Be(targetName);
        }

        [Theory]
        [InlineData("postcompile", "Build")]
        [InlineData("postpublish", "Publish")]
        public void MigratingPostScriptsPopulatesAfterTargetsWithAppropriateTarget(
            string scriptName,
            string targetName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();
            var commands = new[] { "fakecommand" };

            var target = scriptMigrationRule.MigrateScriptSet(
                mockProj,
                commands,
                scriptName,
                IsMultiTFM);

            target.AfterTargets.Should().Be(targetName);
        }

        [Theory]
        [InlineData("precompile")]
        [InlineData("postcompile")]
        [InlineData("prepublish")]
        [InlineData("postpublish")]
        public void MigratingScriptsWithMultipleCommandsCreatesExecTaskForEach(string scriptName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new[] { "fakecommand1", "fakecommand2", "mockcommand3" };
            var commandsInTask = commands.ToDictionary(c => c, c => false);

            var target = scriptMigrationRule.MigrateScriptSet(
                mockProj,
                commands,
                scriptName,
                IsMultiTFM);

            foreach (var task in target.Tasks)
            {
                var taskCommand = task.GetParameter("Command");
                var originalCommandCandidates = commands.Where(c => taskCommand.Contains(c));
                originalCommandCandidates.Count().Should().Be(1);

                var command = originalCommandCandidates.First();
                commandsInTask[command]
                    .Should().Be(false, "Expected to find each element from commands Array once");

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
        public void MigratedScriptSetHasExecAndReplacesVariables(string scriptName)
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new[] { "%compile:FullTargetFramework%", "%compile:Configuration%"};

            var target = scriptMigrationRule.MigrateScriptSet(
                mockProj,
                commands,
                scriptName,
                IsMultiTFM);

            target.Tasks.Count().Should().Be(commands.Length);

            foreach (var task in target.Tasks)
            {
                var taskCommand = task.GetParameter("Command");
                var commandIndex = Array.IndexOf(commands, taskCommand);

                commandIndex.Should().Be(
                    -1,
                    "Expected command array elements to be replaced by appropriate msbuild properties");
            }
        }

        [Fact]
        public void PublishIISCommandDoesNotGetMigratedBecauseItIsNowInTheWebSDK()
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new[]
            {
                "dotnet publish-iis --publish-folder %publish:OutputPath% --framework %publish:FullTargetFramework%"
            };

            var target = scriptMigrationRule.MigrateScriptSet(
                mockProj,
                commands,
                "postpublish",
                IsMultiTFM);
            target.Tasks.Should().BeEmpty();
        }

        [Fact]
        public void FormattingScriptCommandsReplacesUnknownVariablesWithMSBuildPropertyForEnvironmentVariableSupport()
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            scriptMigrationRule.ReplaceScriptVariables($"%UnknownVariable%").Should().Be("$(UnknownVariable)");
        }

        [Fact]
        public void MigratingScriptsWithMultiTFMCreatesTargetWithIsCrossTargettingBuildNotEqualTrueCondition()
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new[] { "compile:FullTargetFramework", "compile:Configuration"};

            var target = scriptMigrationRule.MigrateScriptSet(
                mockProj,
                commands,
                "prepublish",
                IsMultiTFM);
            target.Condition.Should().Be(" '$(IsCrossTargetingBuild)' != 'true' ");
        }

        [Fact]
        public void MigratingScriptsWithSingleTFMDoesNotCreateTargetWithIsCrossTargettingBuild()
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new[] { "compile:FullTargetFramework", "compile:Configuration"};

            var target = scriptMigrationRule.MigrateScriptSet(
                mockProj,
                commands,
                "prepublish",
                false);
            target.Condition.Should().BeEmpty();
        }

        [Fact]
        public void MigratingScriptsThrowsOnInvalidScriptSet()
        {
            var scriptMigrationRule = new MigrateScriptsRule();
            ProjectRootElement mockProj = ProjectRootElement.Create();

            var commands = new string[] { "fakecommand" };

            Action action = () => scriptMigrationRule.MigrateScriptSet(
                mockProj,
                commands,
                "invalidScriptSet",
                IsMultiTFM);

            action.ShouldThrow<MigrationException>()
                .WithMessage("MIGRATE1019::Unsupported Script Event Hook: invalidScriptSet is an unsupported script event hook for project migration");
        }
    }
}
