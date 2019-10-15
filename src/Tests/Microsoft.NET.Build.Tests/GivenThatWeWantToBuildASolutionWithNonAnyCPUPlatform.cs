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

        [Fact]
        public void It_builds_solution_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("x64SolutionBuild")
                .WithSource()
                .Restore(Log);


            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, "x64SolutionBuild.sln");
            buildCommand
                .Execute()
                .Should()
                .Pass();

            buildCommand.GetOutputDirectory("netcoreapp1.1", Path.Combine("x64", "Debug"))
                .Should()
                .OnlyHaveFiles(new[] {
                    "x64SolutionBuild.runtimeconfig.dev.json",
                    "x64SolutionBuild.runtimeconfig.json",
                    "x64SolutionBuild.deps.json",
                    "x64SolutionBuild.dll",
                    "x64SolutionBuild.pdb"
                });
        }
    }
}
