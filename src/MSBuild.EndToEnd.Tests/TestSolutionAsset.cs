// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.EndToEndTests
{
    /// <summary>
    /// Represents a test solution asset.
    /// </summary>
    internal readonly struct TestSolutionAsset
    {
        // Solution folder containing the test asset
        internal string SolutionFolder { get; }

        // Path to main (entry) project file relative to the solution folder
        internal string ProjectRelativePath { get; }

        internal TestSolutionAsset(string solutionFolder, string projectFile)
        {
            SolutionFolder = solutionFolder;
            ProjectRelativePath = projectFile;
        }

        /// <summary>
        /// Gets the path to the project file. This is relative when used as a test asset definition,
        /// or absolute when used as an isolated test instance (after PrepareIsolatedTestAssets).
        /// </summary>
        internal string ProjectPath => Path.Combine(SolutionFolder, ProjectRelativePath);
    }
}
