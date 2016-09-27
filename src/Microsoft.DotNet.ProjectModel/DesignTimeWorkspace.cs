// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel
{
    public class DesignTimeWorkspace : Workspace
    {
        private readonly HashSet<string> _projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool _needRefresh;

        public DesignTimeWorkspace(ProjectReaderSettings settings) : base(settings, true) { }

        public void AddProject(string path)
        {
            var projectPath = ProjectPathHelper.NormalizeProjectDirectoryPath(path);

            if (projectPath != null)
            {
                _needRefresh = _projects.Add(path);
            }
        }

        public void RemoveProject(string path)
        {
            _needRefresh = _projects.Remove(path);
        }

        /// <summary>
        /// Refresh all cached projects in the Workspace
        /// </summary>
        public void Refresh()
        {
            if (!_needRefresh)
            {
                return;
            }

            var basePaths = new List<string>(_projects);
            _projects.Clear();

            foreach (var projectDirectory in basePaths)
            {
                var project = GetProject(projectDirectory);
                if (project == null)
                {
                    continue;
                }

                _projects.Add(project.ProjectDirectory);

                foreach (var projectContext in GetProjectContextCollection(project.ProjectDirectory).ProjectContexts)
                {
                    foreach (var reference in GetProjectReferences(projectContext))
                    {
                        var referencedProject = GetProject(reference.Path);
                        if (referencedProject != null)
                        {
                            _projects.Add(referencedProject.ProjectDirectory);
                        }
                    }
                }
            }

            _needRefresh = false;
        }

        protected override IEnumerable<ProjectContext> BuildProjectContexts(Project project)
        {
            foreach (var framework in project.GetTargetFrameworks())
            {
                yield return CreateBaseProjectBuilder(project)
                    .AsDesignTime()
                    .WithTargetFramework(framework.FrameworkName)
                    .Build();
            }
        }

        private static List<string> ResolveProjectPath(string projectPath)
        {
            if (File.Exists(projectPath))
            {
                var filename = Path.GetFileName(projectPath);
                if (!Project.FileName.Equals(filename, StringComparison.OrdinalIgnoreCase) &&
                    !GlobalSettings.FileName.Equals(filename, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                projectPath = Path.GetDirectoryName(projectPath);
            }

            if (File.Exists(Path.Combine(projectPath, Project.FileName)))
            {
                return new List<string> { projectPath };
            }

            if (File.Exists(Path.Combine(projectPath, GlobalSettings.FileName)))
            {
                var root = ProjectRootResolver.ResolveRootDirectory(projectPath);
                GlobalSettings globalSettings;
                if (GlobalSettings.TryGetGlobalSettings(projectPath, out globalSettings))
                {
                    return globalSettings.ProjectSearchPaths
                                         .Select(searchPath => Path.Combine(globalSettings.DirectoryPath, searchPath))
                                         .Where(actualPath => Directory.Exists(actualPath))
                                         .SelectMany(actualPath => Directory.GetDirectories(actualPath))
                                         .Where(actualPath => File.Exists(Path.Combine(actualPath, Project.FileName)))
                                         .Select(path => Path.GetFullPath(path))
                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                         .ToList();
                }
            }

            return null;
        }

        private static IEnumerable<ProjectDescription> GetProjectReferences(ProjectContext context)
        {
            var projectDescriptions = context.LibraryManager
                                             .GetLibraries()
                                             .Where(lib => lib.Identity.Type == LibraryType.Project)
                                             .OfType<ProjectDescription>();

            foreach (var description in projectDescriptions)
            {
                if (description.Identity.Name == context.ProjectFile.Name)
                {
                    continue;
                }

                // if this is an assembly reference then don't threat it as project reference
                if (!string.IsNullOrEmpty(description.TargetFrameworkInfo?.AssemblyPath))
                {
                    continue;
                }

                yield return description;
            }
        }
    }
}
