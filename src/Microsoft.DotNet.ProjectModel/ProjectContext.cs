// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectContext
    {
        public GlobalSettings GlobalSettings { get; }

        public ProjectDescription RootProject { get; }

        public NuGetFramework TargetFramework { get; }

        public string RuntimeIdentifier { get; }

        public Project ProjectFile => RootProject.Project;

        public LockFile LockFile { get; }

        public string RootDirectory => GlobalSettings?.DirectoryPath;

        public string ProjectDirectory => ProjectFile.ProjectDirectory;

        public string PackagesDirectory { get; }

        public LibraryManager LibraryManager { get; }

        internal ProjectContext(
            GlobalSettings globalSettings,
            ProjectDescription rootProject,
            NuGetFramework targetFramework,
            string runtimeIdentifier,
            string packagesDirectory,
            LibraryManager libraryManager,
            LockFile lockfile)
        {
            GlobalSettings = globalSettings;
            RootProject = rootProject;
            TargetFramework = targetFramework;
            RuntimeIdentifier = runtimeIdentifier;
            PackagesDirectory = packagesDirectory;
            LibraryManager = libraryManager;
            LockFile = lockfile;
        }

        public LibraryExporter CreateExporter(string configuration, string buildBasePath = null)
        {
            return new LibraryExporter(RootProject, LibraryManager, configuration, RuntimeIdentifier, buildBasePath, RootDirectory);
        }

        /// <summary>
        /// Creates a project context for the project located at <paramref name="projectPath"/>,
        /// specifically in the context of the framework specified in <paramref name="framework"/>
        /// </summary>
        public static ProjectContext Create(string projectPath, NuGetFramework framework)
        {
            return Create(projectPath, framework, Enumerable.Empty<string>());
        }

        /// <summary>
        /// Creates a project context for the project located at <paramref name="projectPath"/>,
        /// specifically in the context of the framework specified in <paramref name="framework"/>
        /// and the candidate runtime identifiers specified in <param name="runtimeIdentifiers"/>
        /// </summary>
        public static ProjectContext Create(string projectPath, NuGetFramework framework, IEnumerable<string> runtimeIdentifiers)
        {
            if (projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }
            return new ProjectContextBuilder()
                        .WithProjectDirectory(projectPath)
                        .WithTargetFramework(framework)
                        .WithRuntimeIdentifiers(runtimeIdentifiers)
                        .Build();
        }

        public static ProjectContextBuilder CreateBuilder(string projectPath, NuGetFramework framework)
        {
            if (projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }
            return new ProjectContextBuilder()
                        .WithProjectDirectory(projectPath)
                        .WithTargetFramework(framework);
        }
        
        /// <summary>
        /// Creates a project context for each framework located in the project at <paramref name="projectPath"/>
        /// </summary>
        public static IEnumerable<ProjectContext> CreateContextForEachFramework(string projectPath, ProjectReaderSettings settings = null, IEnumerable<string> runtimeIdentifiers = null)
        {
            if (!projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.Combine(projectPath, Project.FileName);
            }
            var project = ProjectReader.GetProject(projectPath, settings);

            foreach (var framework in project.GetTargetFrameworks())
            {
                yield return new ProjectContextBuilder()
                                .WithProject(project)
                                .WithTargetFramework(framework.FrameworkName)
                                .WithReaderSettings(settings)
                                .WithRuntimeIdentifiers(runtimeIdentifiers ?? Enumerable.Empty<string>())
                                .Build();
            }
        }

        /// <summary>
        /// Creates a project context for each target located in the project at <paramref name="projectPath"/>
        /// </summary>
        public static IEnumerable<ProjectContext> CreateContextForEachTarget(string projectPath, ProjectReaderSettings settings = null)
        {
            var project = ProjectReader.GetProject(projectPath);

            return new ProjectContextBuilder()
                        .WithReaderSettings(settings)
                        .WithProject(project)
                        .BuildAllTargets();
        }


        /// <summary>
        /// Creates a project context based on existing context but using runtime target
        /// </summary>
        /// <param name="context"></param>
        /// <param name="runtimeIdentifiers"></param>
        /// <returns></returns>

        public ProjectContext CreateRuntimeContext(IEnumerable<string> runtimeIdentifiers)
        {
            // Temporary until we have removed RID inference from NuGet
            if(TargetFramework.IsCompileOnly)
            {
                return this;
            }

            // Check if there are any runtime targets (i.e. are we portable)
            var standalone = LockFile.Targets
                .Where(t => t.TargetFramework.Equals(TargetFramework))
                .Any(t => !string.IsNullOrEmpty(t.RuntimeIdentifier));

            var context = Create(ProjectFile.ProjectFilePath, TargetFramework, standalone ? runtimeIdentifiers : Enumerable.Empty<string>());
            if (standalone && context.RuntimeIdentifier == null)
            {
                // We are standalone, but don't support this runtime
                var rids = string.Join(", ", runtimeIdentifiers);
                throw new InvalidOperationException($"Can not find runtime target for framework '{TargetFramework}' compatible with one of the target runtimes: '{rids}'. " +
                                                    "Possible causes:" + Environment.NewLine +
                                                    "1. The project has not been restored or restore failed - run `dotnet restore`" + Environment.NewLine +
                                                    $"2. The project does not list one of '{rids}' in the 'runtimes' section.");
            }
            return context;
        }

        public OutputPaths GetOutputPaths(string configuration, string buidBasePath = null, string outputPath = null)
        {
            return OutputPathsCalculator.GetOutputPaths(ProjectFile,
                TargetFramework,
                RuntimeIdentifier,
                configuration,
                RootDirectory,
                buidBasePath,
                outputPath);
        }
    }
}
