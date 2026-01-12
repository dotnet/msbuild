// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.UnitTests.Shared;

#nullable disable

namespace Microsoft.Build.EndToEndTests
{
    /// <summary>
    /// Fixture for test assets that handles expensive initialization like NuGet restore.
    /// </summary>
    public class TestAssetsFixture : IDisposable
    {
        public string TestAssetDir { get; }

        // Public constants for common project paths
        public const string SingleProjectPath = "SingleProject\\SingleProject.csproj";
        public const string ProjectWithDependencies = "ProjectWithDependencies\\ConsoleApp\\ConsoleApp.csproj";

        private static readonly string[] ProjectsToRestore = 
        [
            SingleProjectPath,
            ProjectWithDependencies
        ];

        public TestAssetsFixture()
        {
            TestAssetDir = Path.Combine(Path.GetDirectoryName(typeof(TestAssetsFixture).Assembly.Location) ?? AppContext.BaseDirectory, "TestAssets");
            RestoreTestAssets();
        }

        private void RestoreTestAssets()
        {
            foreach (string projectPath in ProjectsToRestore)
            {
                string fullPath = Path.Combine(TestAssetDir, projectPath);
                
                if (File.Exists(fullPath))
                {
                    RunnerUtilities.ExecBootstrapedMSBuild($"\"{fullPath}\" /t:Restore /v:minimal", out bool success);
                    if (!success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Failed to restore {fullPath}");
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
