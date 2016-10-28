// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetMSBuildBuildsProjects : TestBase
    {
        [Fact]
        public void ItRunsSpecifiedTargetsWithPropertiesCorrectly()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("MSBuildBareBonesProject");

            var testProjectDirectory = testInstance.TestRoot;

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayHello")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello, from MSBuild!");

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayGoodbye")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Goodbye, from MSBuild. :'(");

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayThis /p:This=GreatScott")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("You want me to say 'GreatScott'");
        }

        [Theory]
        [InlineData("build", true)]
        [InlineData("clean", true)]
        [InlineData("pack", true)]
        [InlineData("publish", true)]
        [InlineData("restore", true)]
        [InlineData("run", true)]
        public void When_help_is_invoked_Then_MSBuild_extra_options_text_is_included_in_output(string commandName, bool isMSBuildCommand)
        {
            const string MSBuildHelpText = "  Any extra options that should be passed to MSBuild. See 'dotnet msbuild -h' for available options.";

            var projectDirectory = TestAssetsManager.CreateTestDirectory("ItContainsMSBuildHelpText");
            var result = new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory.Path)
                .ExecuteWithCapturedOutput($"{commandName} --help");
            
            result.ExitCode.Should().Be(0);
            if (isMSBuildCommand)
            {
                result.StdOut.Should().Contain(MSBuildHelpText);
            }
            else
            {
                result.StdOut.Should().NotContain(MSBuildHelpText);
            }
        }

    }
}
