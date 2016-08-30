// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Publish.Tests
{
    public class GivenThatWeWantToPreserveCompilationContext
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_publishes_the_project_with_a_refs_folder_and_correct_deps_file()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("TestAppCompilationContext")
                .WithSource()
                .Restore("--fallbacksource", $"{RepoInfo.PackagesPath}");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);

            publishCommand
                .Execute()
                .Should()
                .Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestApp.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibrary.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("Newtonsoft.Json.dll");

            var refsDirectory = new DirectoryInfo(Path.Combine(publishCommand.GetOutputDirectory().FullName, "refs"));
            // Should have compilation time assemblies
            refsDirectory.Should().HaveFile("System.IO.dll");
            // Libraries in which lib==ref should be deduped
            refsDirectory.Should().NotHaveFile("TestLibrary.dll");
            refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");

            // TODO verify the deps file
        }
    }
}