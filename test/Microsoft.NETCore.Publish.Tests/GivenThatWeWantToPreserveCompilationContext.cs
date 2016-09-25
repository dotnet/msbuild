// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
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
                .CopyTestAsset("CompilationContext", "PreserveCompilationContext")
                .WithSource();

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");
            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            testAsset.Restore(appProjectDirectory, $"/p:RestoreFallbackFolders={RepoInfo.PackagesPath}");
            testAsset.Restore(libraryProjectDirectory, $"/p:RestoreFallbackFolders={RepoInfo.PackagesPath}");

            var publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);

            publishCommand
                .Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().HaveFiles(new[] {
                "TestApp.dll",
                "TestLibrary.dll",
                "Newtonsoft.Json.dll"});

            var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));
            // Should have compilation time assemblies
            refsDirectory.Should().HaveFile("System.IO.dll");
            // Libraries in which lib==ref should be deduped
            refsDirectory.Should().NotHaveFile("TestLibrary.dll");
            refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                dependencyContext.CompilationOptions.Defines.Should().BeEquivalentTo(new[] { "DEBUG", "TRACE" });
                dependencyContext.CompilationOptions.LanguageVersion.Should().Be("");
                dependencyContext.CompilationOptions.Platform.Should().Be("AnyCPU");
                dependencyContext.CompilationOptions.Optimize.Should().Be(false);
                dependencyContext.CompilationOptions.KeyFile.Should().Be("");
                dependencyContext.CompilationOptions.EmitEntryPoint.Should().Be(true);
                dependencyContext.CompilationOptions.DebugType.Should().Be("portable");

                dependencyContext.CompileLibraries.Count.Should().Be(115);
            }
        }
    }
}