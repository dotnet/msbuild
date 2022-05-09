// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASolutionWithNonAnyCPUPlatform : SdkTest
    {
        public GivenThatWeWantToBuildASolutionWithNonAnyCPUPlatform(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.1.0.60101")]
        public void It_builds_solution_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("x64SolutionBuild")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset, "x64SolutionBuild.sln");
            buildCommand
                .Execute("/p:ProduceReferenceAssembly=false")
                .Should()
                .Pass();

            buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework, Path.Combine("x64", "Debug"))
                .Should()
                .OnlyHaveFiles(new[] {
                    "x64SolutionBuild.runtimeconfig.json",
                    "x64SolutionBuild.deps.json",
                    "x64SolutionBuild.dll",
                    "x64SolutionBuild.pdb",
                    $"x64SolutionBuild{EnvironmentInfo.ExecutableExtension}"
                });
        }
    }
}
