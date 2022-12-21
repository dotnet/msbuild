// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;
using System.Runtime.CompilerServices;

namespace dotnet.Tests
{
    public class OutputPathOptionTests : SdkTest
    {
        public OutputPathOptionTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("pack")]
        [InlineData("publish")]
        [InlineData("test")]
        public void OutputOptionGeneratesErrorsWithSolutionFiles(string command)
        {
            TestOutputWithSolution(command, true);
        }

        [Theory]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("pack")]
        [InlineData("publish")]
        [InlineData("test")]
        public void OutputPathPropertyDoesNotGenerateErrorsWithSolutionFiles(string command)
        {
            TestOutputWithSolution(command, false);
        }

        void TestOutputWithSolution(string command, bool useOption, [CallerMemberName] string callingMethod = "")
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

            if (useOption)
            {
                new DotnetCommand(Log)
                    .WithWorkingDirectory(slnDirectory)
                    .Execute(command, "--output", outputDirectory)
                    .Should()
                    .Fail()
                    .And
                    .HaveStdOutContaining("NETSDK1194");
            }
            else
            {
                new DotnetCommand(Log)
                    .WithWorkingDirectory(slnDirectory)
                    .Execute(command, $"--property:OutputPath={outputDirectory}")
                    .Should()
                    .Pass();
            }
        }
    }
}
