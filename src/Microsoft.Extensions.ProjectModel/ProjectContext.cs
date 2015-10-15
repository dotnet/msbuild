// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Extensions.ProjectModel.Compilation;
using Microsoft.Extensions.ProjectModel.Graph;
using Microsoft.Extensions.ProjectModel.Resolution;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel
{
    // NOTE(anurse): Copied from ApplicationHostContext in DNX. This name seemed more appropriate for this :)
    public class ProjectContext
    {

        public GlobalSettings GlobalSettings { get; }

        public ProjectDescription RootProject { get; }

        public NuGetFramework TargetFramework { get; }

        public string RuntimeIdentifier { get; }

        public Project ProjectFile => RootProject.Project;

        public string RootDirectory => GlobalSettings.DirectoryPath;

        public string ProjectDirectory => ProjectFile.ProjectDirectory;

        public string PackagesDirectory { get; }

        public FrameworkReferenceResolver FrameworkResolver { get; }

        public LibraryManager LibraryManager { get; }

        internal ProjectContext(
            GlobalSettings globalSettings,
            ProjectDescription rootProject,
            NuGetFramework targetFramework,
            string runtimeIdentifier,
            string packagesDirectory,
            FrameworkReferenceResolver frameworkResolver,
            LibraryManager libraryManager)
        {
            GlobalSettings = globalSettings;
            RootProject = rootProject;
            TargetFramework = targetFramework;
            RuntimeIdentifier = runtimeIdentifier;
            PackagesDirectory = packagesDirectory;
            FrameworkResolver = frameworkResolver;
            LibraryManager = libraryManager;
        }

        public LibraryExporter CreateExporter(string configuration)
        {
            return new LibraryExporter(RootProject, LibraryManager, configuration);
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
            if(projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }
            return new ProjectContextBuilder()
            {
                ProjectDirectory = projectPath,
                TargetFramework = framework,
                RuntimeIdentifiers = runtimeIdentifiers
            }.Build();
        }

        /// <summary>
        /// Creates a project context for each framework located in the project at <paramref name="projectPath"/>
        /// </summary>
        public static IEnumerable<ProjectContext> CreateContextForEachFramework(string projectPath)
        {
            if(!projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.Combine(projectPath, Project.FileName);
            }
            var project = ProjectReader.GetProject(projectPath);

            foreach(var framework in project.GetTargetFrameworks())
            {
                yield return new ProjectContextBuilder()
                {
                    Project = project,
                    TargetFramework = framework.FrameworkName
                }.Build();
            }
        }
    }
}