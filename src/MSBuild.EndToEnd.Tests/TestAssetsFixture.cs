// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.UnitTests.Shared;

#nullable disable

namespace Microsoft.Build.EndToEndTests
{
    /// <summary>
    /// Fixture for test solution assets that handles expensive initialization like NuGet restore.
    /// </summary>
    public class TestSolutionAssetsFixture : IDisposable
    {
        public string TestAssetDir { get; }

        // Test solution asset definitions
        public static readonly TestSolutionAsset SingleProject = new("SingleProject", "SingleProject.csproj");
        public static readonly TestSolutionAsset ProjectWithDependencies = new("ProjectWithDependencies", "ConsoleApp\\ConsoleApp.csproj");

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
                
                if (File.Exists(projectPath))
                {
                    RunnerUtilities.ExecBootstrapedMSBuild($"\"{projectPath}\" /t:Restore /v:minimal", out bool success);
                    if (!success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Failed to restore {projectPath}");
                    }
                }
            }
        }

        public void Dispose()
        {
            // Clean up if needed
        }
    }
}
