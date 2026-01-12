// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

#nullable disable

namespace Microsoft.Build.EndToEndTests
{
    /// <summary>
    /// Represents a test solution asset.
    /// </summary>
    public readonly struct TestSolutionAsset
    {
        // Solution folder containing the test asset
        public string SolutionFolder { get; }

        // Path to main (entry) project file relative to the solution folder
        public string ProjectRelativePath { get; }
        
        public TestSolutionAsset(string solutionFolder, string projectFile)
        {
            SolutionFolder = solutionFolder;
            ProjectRelativePath = projectFile;
        }
        
        /// <summary>
        /// Gets the full relative path from TestAssets root to the project file.
        /// </summary>
        public string ProjectPath => Path.Combine(SolutionFolder, ProjectRelativePath);
    }
}