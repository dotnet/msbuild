using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatAProjectHasntBeenRestored : SdkTest
    {
        public GivenThatAProjectHasntBeenRestored(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("TestLibrary", null)]
        [InlineData("TestApp", null)]
        [InlineData("TestApp", "netcoreapp2.1")]
        [InlineData("TestApp", ToolsetInfo.CurrentTargetFramework)]
        public void The_build_fails_if_nuget_restore_has_not_occurred(string relativeProjectPath, string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", identifier: relativeProjectPath + "_" + targetFramework ?? string.Empty)
                .WithSource()
                .WithTargetFramework(targetFramework, relativeProjectPath);

            var projectDirectory = Path.Combine(testAsset.TestRoot, relativeProjectPath);

            VerifyNotRestoredFailure(projectDirectory);
        }

        private void VerifyNotRestoredFailure(string projectDirectory)
        {
            var buildCommand = new BuildCommand(Log, projectDirectory);

            var assetsFile = Path.Combine(buildCommand.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            buildCommand
                //  Pass "/clp:summary" so that we can check output for string "1 Error(s)"
                .ExecuteWithoutRestore("/clp:summary")
                .Should()
                .Fail()
                .And.HaveStdOutContaining(assetsFile)
                //  We should only get one error
                .And.HaveStdOutContaining("1 Error(s)");
        }

        [Fact]
        public void ReadingCacheDoesNotFail()
        {
            var testProject = new TestProject()
            {
                Name = "App",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1062");
        }
    }
}
