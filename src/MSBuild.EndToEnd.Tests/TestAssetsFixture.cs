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
    /// Restore uses bootstrap MSBuild so it exercises the same code path as the tests themselves,
    /// which is important when restore-related tasks become multithreadable.
    /// </summary>
    public class TestSolutionAssetsFixture
    {
        public string TestAssetDir { get; }

        // Test solution asset definitions
        public static readonly TestSolutionAsset SingleProject = new("SingleProject", "SingleProject.csproj");
        public static readonly TestSolutionAsset ProjectWithDependencies = new("ProjectWithDependencies", "ConsoleApp/ConsoleApp.csproj");
        public static readonly TestSolutionAsset NonSdkSingleProject = new("NonSdkSingleProject", "NonSdkSingleProject.csproj");
        public static readonly TestSolutionAsset NonSdkProjectWithDependencies = new("NonSdkProjectWithDependencies", "ConsoleApp/ConsoleApp.csproj");

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
