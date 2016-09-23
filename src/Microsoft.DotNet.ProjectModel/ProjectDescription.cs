// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectDescription : LibraryDescription
    {
        // Create an unresolved project description
        public ProjectDescription(string name, string path)
            : base(
                  new LibraryIdentity(name, null, LibraryType.Project),
                  string.Empty, // Projects don't have hashes
                  path,
                  Enumerable.Empty<ProjectLibraryDependency>(),
                  framework: null,
                  resolved: false,
                  compatible: false)
        {
        }

        public ProjectDescription(
            LibraryRange libraryRange,
            Project project,
            IEnumerable<ProjectLibraryDependency> dependencies,
            TargetFrameworkInformation targetFrameworkInfo,
            bool resolved) :
                base(
                    new LibraryIdentity(project.Name, project.Version, LibraryType.Project),
                    string.Empty, // Projects don't have hashes
                    project.ProjectFilePath,
                    dependencies,
                    targetFrameworkInfo.FrameworkName,
                    resolved,
                    compatible: true)
        {
            Project = project;
            TargetFrameworkInfo = targetFrameworkInfo;
        }

        public Project Project { get; }

        public TargetFrameworkInformation TargetFrameworkInfo { get; }
        
        public OutputPaths GetOutputPaths(string buildBasePath, string solutionRootPath, string configuration, string runtime)
        {
            return OutputPathsCalculator.GetOutputPaths(Project,
                Framework,
                runtimeIdentifier: runtime,
                configuration: configuration,
                solutionRootPath: solutionRootPath,
                buildBasePath: buildBasePath,
                outputPath: null);
        }
    }
}
