using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
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
        [InlineData("TestApp", "netcoreapp3.0")]
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
                .Execute("/clp:summary")
                .Should()
                .Fail()
                .And.HaveStdOutContaining(assetsFile)
                //  We should only get one error
                .And.HaveStdOutContaining("1 Error(s)");
        }
    }
}
