// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;

namespace Microsoft.DotNet.ProjectModel
{
    public class WorkspaceContext
    {
        // key: project directory
        private readonly ConcurrentDictionary<string, FileModelEntry<Project>> _projectsCache
                   = new ConcurrentDictionary<string, FileModelEntry<Project>>();

        // key: project directory
        private readonly ConcurrentDictionary<string, FileModelEntry<LockFile>> _lockFileCache
                   = new ConcurrentDictionary<string, FileModelEntry<LockFile>>();

        // key: project directory, target framework
        private readonly ConcurrentDictionary<string, ProjectContextCollection> _projectContextsCache
                   = new ConcurrentDictionary<string, ProjectContextCollection>();

        private readonly HashSet<string> _projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool _needRefresh;

        private WorkspaceContext(IEnumerable<string> projectPaths)
        {
            foreach (var path in projectPaths)
            {
                AddProject(path);
            }

            Refresh();
        }

        /// <summary>
        /// Create a WorkspaceContext from a given path.
        /// 
        /// There must be either a global.json or project.json at under the given path. Otherwise
        /// null is returned.
        /// 
        /// If the given path points to a global.json, all the projects found under the search paths
        /// are added to the WorkspaceContext.
        /// 
        /// If the given path points to a project.json, all the projects it referenced as well as itself
        /// are added to the WorkspaceContext.
        /// </summary>
        public static WorkspaceContext CreateFrom(string projectPath)
        {
            var projectPaths = ResolveProjectPath(projectPath);
            if (projectPaths == null || !projectPaths.Any())
            {
                return null;
            }

            var context = new WorkspaceContext(projectPaths);
            return context;
        }

        public static WorkspaceContext Create()
        {
            return new WorkspaceContext(Enumerable.Empty<string>());
        }

        public void AddProject(string path)
        {
            var projectPath = NormalizeProjectPath(path);

            if (projectPath != null)
            {
                _needRefresh = _projects.Add(path);
            }
        }

        public void RemoveProject(string path)
        {
            _needRefresh = _projects.Remove(path);
        }

        public IReadOnlyList<string> GetAllProjects()
        {
            Refresh();
            return _projects.ToList().AsReadOnly();
        }

        /// <summary>
        /// Refresh the WorkspaceContext to update projects collection
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
                var project = GetProject(projectDirectory).Model;
                if (project == null)
                {
                    continue;
                }

                _projects.Add(project.ProjectDirectory);

                foreach (var projectContext in GetProjectContexts(project.ProjectDirectory))
                {
                    foreach (var reference in GetProjectReferences(projectContext))
                    {
                        var referencedProject = GetProject(reference.Path).Model;
                        if (referencedProject != null)
                        {
                            _projects.Add(referencedProject.ProjectDirectory);
                        }
                    }
                }
            }

            _needRefresh = false;
        }

        public IReadOnlyList<ProjectContext> GetProjectContexts(string projectPath)
        {
            return GetProjectContextCollection(projectPath).ProjectContexts;
        }

        public ProjectContextCollection GetProjectContextCollection(string projectPath)
        {
            return _projectContextsCache.AddOrUpdate(
                projectPath,
                key => AddProjectContextEntry(key, null),
                (key, oldEntry) => AddProjectContextEntry(key, oldEntry));
        }

        private FileModelEntry<Project> GetProject(string projectDirectory)
        {
            return _projectsCache.AddOrUpdate(
                projectDirectory,
                key => AddProjectEntry(key, null),
                (key, oldEntry) => AddProjectEntry(key, oldEntry));
        }

        private LockFile GetLockFile(string projectDirectory)
        {
            return _lockFileCache.AddOrUpdate(
                projectDirectory,
                key => AddLockFileEntry(key, null),
                (key, oldEntry) => AddLockFileEntry(key, oldEntry)).Model;
        }

        private FileModelEntry<Project> AddProjectEntry(string projectDirectory, FileModelEntry<Project> currentEntry)
        {
            if (currentEntry == null)
            {
                currentEntry = new FileModelEntry<Project>();
            }
            else if (!File.Exists(Path.Combine(projectDirectory, Project.FileName)))
            {
                // project was deleted
                currentEntry.Reset();
                return currentEntry;
            }

            if (currentEntry.IsInvalid)
            {
                Project project;
                if (!ProjectReader.TryGetProject(projectDirectory, out project, currentEntry.Diagnostics))
                {
                    currentEntry.Reset();
                }
                else
                {
                    currentEntry.Model = project;
                    currentEntry.FilePath = project.ProjectFilePath;
                    currentEntry.UpdateLastWriteTime();
                }
            }

            return currentEntry;
        }

        private FileModelEntry<LockFile> AddLockFileEntry(string projectDirectory, FileModelEntry<LockFile> currentEntry)
        {
            if (currentEntry == null)
            {
                currentEntry = new FileModelEntry<LockFile>();
            }

            if (currentEntry.IsInvalid)
            {
                currentEntry.Reset();

                if (!File.Exists(Path.Combine(projectDirectory, LockFile.FileName)))
                {
                    return currentEntry;
                }
                else
                {
                    currentEntry.FilePath = Path.Combine(projectDirectory, LockFile.FileName);
                    currentEntry.Model = LockFileReader.Read(currentEntry.FilePath);
                    currentEntry.UpdateLastWriteTime();
                }
            }

            return currentEntry;
        }

        private ProjectContextCollection AddProjectContextEntry(string projectDirectory,
                                                                ProjectContextCollection currentEntry)
        {
            if (currentEntry == null)
            {
                // new entry required
                currentEntry = new ProjectContextCollection();
            }

            var projectEntry = GetProject(projectDirectory);
            if (projectEntry.Model == null)
            {
                // project doesn't exist anymore
                currentEntry.Reset();
                return currentEntry;
            }

            var project = projectEntry.Model;
            if (currentEntry.HasChanged)
            {
                currentEntry.Reset();

                foreach (var framework in project.GetTargetFrameworks())
                {
                    var builder = new ProjectContextBuilder()
                        .WithProjectResolver(path => GetProject(path).Model)
                        .WithLockFileResolver(path => GetLockFile(path))
                        .WithProject(project)
                        .WithTargetFramework(framework.FrameworkName);

                    currentEntry.ProjectContexts.Add(builder.Build());
                }

                currentEntry.Project = project;
                currentEntry.ProjectFilePath = project.ProjectFilePath;
                currentEntry.LastProjectFileWriteTime = File.GetLastWriteTime(currentEntry.ProjectFilePath);

                var lockFilePath = Path.Combine(project.ProjectDirectory, LockFile.FileName);
                if (File.Exists(lockFilePath))
                {
                    currentEntry.LockFilePath = lockFilePath;
                    currentEntry.LastLockFileWriteTime = File.GetLastWriteTime(lockFilePath);
                }

                currentEntry.ProjectDiagnostics.AddRange(projectEntry.Diagnostics);
            }

            return currentEntry;
        }

        private class FileModelEntry<TModel> where TModel : class
        {
            private DateTime _lastWriteTime;

            public TModel Model { get; set; }

            public string FilePath { get; set; }

            public List<DiagnosticMessage> Diagnostics { get; } = new List<DiagnosticMessage>();

            public void UpdateLastWriteTime()
            {
                _lastWriteTime = File.GetLastWriteTime(FilePath);
            }

            public bool IsInvalid
            {
                get
                {
                    if (Model == null)
                    {
                        return true;
                    }

                    if (!File.Exists(FilePath))
                    {
                        return true;
                    }

                    return _lastWriteTime < File.GetLastWriteTime(FilePath);
                }
            }

            public void Reset()
            {
                Model = null;
                FilePath = null;
                Diagnostics.Clear();
                _lastWriteTime = DateTime.MinValue;
            }
        }

        private static string NormalizeProjectPath(string path)
        {
            if (File.Exists(path) &&
                string.Equals(Path.GetFileName(path), Project.FileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.GetDirectoryName(path));
            }
            else if (Directory.Exists(path) &&
                     File.Exists(Path.Combine(path, Project.FileName)))
            {
                return Path.GetFullPath(path);
            }

            return null;
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
