// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToRunFromMSBuildTarget : SdkTest
    {
        public GivenThatWeWantToRunFromMSBuildTarget(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_runs_successfully()
        {
            TestProject testProject = new TestProject()
            {
                Name = "TestRunTargetProject",
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var runTargetCommand = new MSBuildCommand(Log, "run", Path.Combine(testAsset.TestRoot, testProject.Name));
            runTargetCommand
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Hello World!");
        }
    }
}
