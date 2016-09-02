// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Build.Tests
{
    public class GivenThatWeWantToBuildALibrary
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_builds_the_library_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource()
                .Restore("--fallbacksource", $"{RepoInfo.PackagesPath}");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory();

            outputDirectory.Should().OnlyHaveFiles(new [] {
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "TestLibrary.deps.json"
            });
        }
    }
}