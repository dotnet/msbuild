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
    public class GivenThatWeWantToBuildACrossTargetedLibrary
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        public void It_builds_the_library_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource();

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            testAsset.Restore(libraryProjectDirectory, $"/p:RestoreFallbackFolders={RepoInfo.PackagesPath}");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory();
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "netstandard1.4/TestLibrary.dll",
                "netstandard1.4/TestLibrary.pdb",
                "netstandard1.4/TestLibrary.deps.json",
                "netstandard1.5/TestLibrary.dll",
                "netstandard1.5/TestLibrary.pdb",
                "netstandard1.5/TestLibrary.deps.json"
            }, SearchOption.AllDirectories);
        }
    }
}
