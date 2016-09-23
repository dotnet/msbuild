// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Resolution;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectContext
    {
        private string[] _runtimeFallbacks;

        public ProjectContextIdentity Identity { get; }

        public GlobalSettings GlobalSettings { get; }

        public ProjectDescription RootProject { get; }

        public NuGetFramework TargetFramework { get; }

        public LibraryDescription PlatformLibrary { get; }

        public bool IsPortable { get; }

        public string RuntimeIdentifier { get; }

        public Project ProjectFile => RootProject?.Project;

        public LockFile LockFile { get; }

        public string RootDirectory => GlobalSettings?.DirectoryPath;

        public string ProjectDirectory => ProjectFile?.ProjectDirectory;

        public string PackagesDirectory { get; }

        public LibraryManager LibraryManager { get; }

        public List<DiagnosticMessage> Diagnostics { get; }

        internal ProjectContext(
            GlobalSettings globalSettings,
            ProjectDescription rootProject,
            LibraryDescription platformLibrary,
            NuGetFramework targetFramework,
            bool isPortable,
            string runtimeIdentifier,
            string packagesDirectory,
            LibraryManager libraryManager,
            LockFile lockfile,
            List<DiagnosticMessage> diagnostics)
        {
            Identity = new ProjectContextIdentity(rootProject?.Path, targetFramework);
            GlobalSettings = globalSettings;
            RootProject = rootProject;
            PlatformLibrary = platformLibrary;
            TargetFramework = targetFramework;
            RuntimeIdentifier = runtimeIdentifier;
            PackagesDirectory = packagesDirectory;
            LibraryManager = libraryManager;
            LockFile = lockfile;
            IsPortable = isPortable;
            Diagnostics = diagnostics;
        }

        public LibraryExporter CreateExporter(string configuration, string buildBasePath = null)
        {
            if (IsPortable && RuntimeIdentifier != null && _runtimeFallbacks == null)
            {
                var graph = RuntimeGraphCollector.Collect(LibraryManager.GetLibraries());
                _runtimeFallbacks = graph.ExpandRuntime(RuntimeIdentifier).ToArray();
            }
            return new LibraryExporter(RootProject,
                LibraryManager,
                configuration,
                RuntimeIdentifier,
                _runtimeFallbacks,
                buildBasePath,
                RootDirectory);
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
                                .WithProjectReaderSettings(settings)
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
                        .WithProjectReaderSettings(settings)
                        .WithProject(project)
                        .BuildAllTargets();
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
