// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAppsWithFrameworkRefs : SdkTest
    {
        public GivenThatWeWantToBuildAppsWithFrameworkRefs(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_builds_the_projects_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();
            VerifyProjectsBuild(testAsset);
        }

        [WindowsOnlyFact]
        public void It_builds_with_disable_implicit_frameworkRefs()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();
            VerifyProjectsBuild(testAsset, "/p:DisableImplicitFrameworkReferences=true");
        }

        void VerifyProjectsBuild(TestAsset testAsset, params string[] buildArgs)
        {
            VerifyBuild(testAsset, "StopwatchLib", "net45", "", buildArgs,
                "StopwatchLib.dll",
                "StopwatchLib.pdb");

            VerifyBuild(testAsset, "EntityFrameworkApp", "net451", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86", buildArgs,
                "EntityFrameworkApp.exe",
                "EntityFrameworkApp.pdb");

            // Try running EntityFrameworkApp.exe
            var buildCommand = new BuildCommand(testAsset, "EntityFrameworkApp");
            var outputDirectory = buildCommand.GetOutputDirectory("net451", runtimeIdentifier: $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86");

            new RunExeCommand(Log, Path.Combine(outputDirectory.FullName, "EntityFrameworkApp.exe"))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Required Test Provider");
        }

        private void VerifyBuild(TestAsset testAsset, string project, string targetFramework, string runtimeIdentifier,
            string [] buildArgs,
            params string [] expectedFiles)
        {
            var buildCommand = new BuildCommand(testAsset, project);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);

            buildCommand
                .Execute(buildArgs)
                .Should()
                .Pass();

            outputDirectory.Should().HaveFiles(expectedFiles);
        }

        [WindowsOnlyFact]
        public void The_clean_target_removes_all_files_from_the_output_folder()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences", "CleanTargetRemovesAll")
                .WithSource();
            VerifyClean(testAsset, "StopwatchLib", "net45", "",
                "StopwatchLib.dll",
                "StopwatchLib.pdb");

            VerifyClean(testAsset, "EntityFrameworkApp", "net451", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86",
                "EntityFrameworkApp.exe",
                "EntityFrameworkApp.pdb");
        }

        private void VerifyClean(TestAsset testAsset, string project, string targetFramework, string runtimeIdentifier,
            params string[] expectedFiles)
        {
            var buildCommand = new BuildCommand(testAsset, project);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().HaveFiles(expectedFiles);

            var cleanCommand = new MSBuildCommand(Log, "Clean", buildCommand.FullPathProjectFile);

            cleanCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(Array.Empty<string>());
        }
    }
}
