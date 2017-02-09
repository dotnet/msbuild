// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.IO;

namespace Microsoft.NET.TestFramework.Commands
{
    public abstract class TestCommand
    {
        public MSBuildTest MSBuild { get; }

        public string ProjectRootPath { get; }

        public string ProjectFile { get; }

        public string FullPathProjectFile => Path.Combine(ProjectRootPath, ProjectFile);

        protected TestCommand(MSBuildTest msBuild, string projectRootPath, string relativePathToProject = null)
        {
            MSBuild = msBuild;
            ProjectRootPath = projectRootPath;
            ProjectFile = FindProjectFile(relativePathToProject);
        }

        public abstract CommandResult Execute(params string[] args);

        private string FindProjectFile(string relativePathToProject)
        {
            if (!string.IsNullOrEmpty(relativePathToProject))
            {
                return Path.Combine(ProjectRootPath, relativePathToProject);
            }

            var buildProjectFiles = Directory.GetFiles(ProjectRootPath, "*.csproj");

            if (buildProjectFiles.Length != 1)
            {
                var errorMsg = $"Found {buildProjectFiles.Length} csproj files under {ProjectRootPath} instead of just 1.";
                throw new ArgumentException(errorMsg);
            }

            return buildProjectFiles[0];
        }

        public virtual DirectoryInfo GetOutputDirectory(string targetFramework, string configuration = "Debug", string runtimeIdentifier = "")
        {
            string output = Path.Combine(ProjectRootPath, "bin", configuration, targetFramework, runtimeIdentifier);
            return new DirectoryInfo(output);
        }

        public DirectoryInfo GetBaseIntermediateDirectory()
        {
            string output = Path.Combine(ProjectRootPath, "obj");
            return new DirectoryInfo(output);
        }
    }
}