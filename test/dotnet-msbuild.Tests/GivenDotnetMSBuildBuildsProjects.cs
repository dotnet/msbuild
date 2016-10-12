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
        // https://github.com/dotnet/cli/issues/4293
        [InlineData("build", false)]
        [InlineData("pack", false)]
        [InlineData("publish", false)]
        [InlineData("restore", false)]
        [InlineData("run", false)]
        [InlineData("build3", true)]
        [InlineData("clean3", true)]
        [InlineData("pack3", true)]
        [InlineData("publish3", true)]
        [InlineData("restore3", true)]
        [InlineData("run3", true)]
        public void ItMSBuildHelpText(string commandName, bool isMSBuildCommand)
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
