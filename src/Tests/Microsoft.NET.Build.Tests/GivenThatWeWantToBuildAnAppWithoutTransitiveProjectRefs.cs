// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithoutTransitiveProjectRefs : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithoutTransitiveProjectRefs(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_the_project_successfully_when_RAR_finds_all_references()
        {
            // NOTE the project dependencies: 1->2->3

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveDependenciesAndTransitiveCompileReference", "BuildWithDisableTransitiveProjectReferences=true")
                .WithSource();

            CommandResult result = RestoreAndBuild(testAsset, out DirectoryInfo outputDirectory);

            result.Should().Pass();

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "1.dll",
                "1.pdb",
                "1.deps.json",
                "1.runtimeconfig.json",
                "1.runtimeconfig.dev.json",
                "2.dll",
                "2.pdb",
                "3.dll",
                "3.pdb",
                "4.dll",
                "4.pdb",
                "5.dll",
                "5.pdb",
            });

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] {Path.Combine(outputDirectory.FullName, "1.dll")})
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World from 1")
                .And
                .HaveStdOutContaining("Hello World from 2")
                .And
                .HaveStdOutContaining("Hello World from 4")
                .And
                .HaveStdOutContaining("Hello World from 5")
                .And
                .HaveStdOutContaining("Hello World from 3")
                .And
                .HaveStdOutContaining("Hello World from 4")
                .And
                .HaveStdOutContaining("Hello World from 5");
        }
        
        [Fact]
        public void It_builds_the_project_successfully_when_RAR_does_not_find_all_references()
        {
            // NOTE the project dependencies: 1->2->3

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveDependenciesButNoTransitiveCompileReference", "BuildWithDisableTransitiveProjectReferences=true")
                .WithSource();

            CommandResult result = RestoreAndBuild(testAsset, out DirectoryInfo outputDirectory);

            result.Should().Pass();

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "1.dll",
                "1.pdb",
                "1.deps.json",
                "1.runtimeconfig.json",
                "1.runtimeconfig.dev.json",
                "2.dll",
                "2.pdb",
            });

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] {Path.Combine(outputDirectory.FullName, "1.dll")})
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World from 1");
        }

        private CommandResult RestoreAndBuild(TestAsset testAsset, out DirectoryInfo outputDirectory)
        {
            testAsset.Restore(Log, "1");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "1");

            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            outputDirectory = buildCommand.GetOutputDirectory("netcoreapp2.1");

            var result = buildCommand
                .Execute("/p:DisableTransitiveProjectReferences=true");
            return result;
        }
    }
}
