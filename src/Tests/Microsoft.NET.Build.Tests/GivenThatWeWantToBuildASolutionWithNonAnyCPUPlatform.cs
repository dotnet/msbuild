// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                .Execute("/p:ProduceReferenceAssembly=false", "/p:UseStandardOutputPaths=false")
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(testAsset.TestRoot, "bin", "x64", "Debug", ToolsetInfo.CurrentTargetFramework))
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
