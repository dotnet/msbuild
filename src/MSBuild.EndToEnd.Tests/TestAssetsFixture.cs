// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;

namespace Microsoft.Build.EndToEndTests
{
    /// <summary>
    /// Fixture for test solution assets that handles expensive initialization like NuGet restore.
    /// Restore runs through bootstrap MSBuild, so failures here can surface real regressions
    /// in the same code paths that the tests exercise.
    /// </summary>
    public class TestSolutionAssetsFixture
    {
        internal string TestAssetDir { get; }

        // Test solution asset definitions
        internal static readonly TestSolutionAsset SingleProject = new("SingleProject", "SingleProject.csproj");
        internal static readonly TestSolutionAsset ProjectWithDependencies = new("ProjectWithDependencies", "ConsoleApp/ConsoleApp.csproj");
        internal static readonly TestSolutionAsset NonSdkSingleProject = new("NonSdkSingleProject", "NonSdkSingleProject.csproj");
        internal static readonly TestSolutionAsset NonSdkProjectWithDependencies = new("NonSdkProjectWithDependencies", "ConsoleApp/ConsoleApp.csproj");

        // Non-SDK projects do not require NuGet restore.
        private static readonly TestSolutionAsset[] AssetsToRestore = 
        [
            SingleProject,
            ProjectWithDependencies
        ];


        public TestSolutionAssetsFixture()
        {
            TestAssetDir = Path.Combine(Path.GetDirectoryName(typeof(TestSolutionAssetsFixture).Assembly.Location) ?? AppContext.BaseDirectory, "TestAssets");
            RestoreTestAssets();
        }

        private void RestoreTestAssets()
        {
            foreach (var asset in AssetsToRestore)
            {
                string projectPath = Path.Combine(TestAssetDir, asset.ProjectPath);
                
                File.Exists(projectPath).ShouldBeTrue($"Test asset project not found: {projectPath}");

                string output = RunnerUtilities.ExecBootstrapedMSBuild($"\"{projectPath}\" /t:Restore /v:minimal", out bool success, timeoutMilliseconds: 120_000);
                success.ShouldBeTrue($"Failed to restore test asset {asset.SolutionFolder}\\{asset.ProjectRelativePath}. Output:\n{output}");
            }
        }
    }
}
