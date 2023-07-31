// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace dotnet.Tests
{
    public class OutputPathOptionTests : SdkTest
    {
        public OutputPathOptionTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("build", true)]
        [InlineData("clean", true)]
        [InlineData("pack", false)]
        [InlineData("publish", true)]
        [InlineData("test", true)]
        public void OutputOptionGeneratesWarningsWithSolutionFiles(string command, bool shouldWarn)
        {
            TestOutputWithSolution(command, useOption: true, shouldWarn: shouldWarn);
        }

        [Theory]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("pack")]
        [InlineData("publish")]
        [InlineData("test")]
        public void OutputPathPropertyDoesNotGenerateWarningsWithSolutionFiles(string command)
        {
            TestOutputWithSolution(command, useOption: false, shouldWarn: false);
        }

        void TestOutputWithSolution(string command, bool useOption, bool shouldWarn, [CallerMemberName] string callingMethod = "")
        {
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, callingMethod, identifier: command);

            var slnDirectory = testAsset.TestRoot;

            Log.WriteLine($"Test root: {slnDirectory}");

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(slnDirectory)
                .Execute("sln")
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(slnDirectory)
                .Execute("sln", "add", testProject.Name)
                .Should().Pass();

            string outputDirectory = Path.Combine(slnDirectory, "bin");
            Microsoft.DotNet.Cli.Utils.CommandResult commandResult;
            if (useOption)
            {
                commandResult = new DotnetCommand(Log)
                    .WithWorkingDirectory(slnDirectory)
                    .Execute(command, "--output", outputDirectory);
            }
            else
            {
                commandResult = new DotnetCommand(Log)
                    .WithWorkingDirectory(slnDirectory)
                    .Execute(command, $"--property:OutputPath={outputDirectory}");
            }
            commandResult.Should().Pass();
            if (shouldWarn)
            {
                commandResult.Should().HaveStdOutContaining("NETSDK1194");
            }
            else
            {
                commandResult.Should().NotHaveStdOutContaining("NETSDK1194");
            }
        }
    }
}
