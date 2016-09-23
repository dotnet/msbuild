// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel
{
    /// <summary>
    /// Represents a cache of Projects, LockFiles, and ProjectContexts
    /// </summary>
    public abstract class Workspace
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

        private readonly ProjectReaderSettings _settings;
        private readonly LockFileFormat _lockFileReader;
        private readonly bool _designTime;

        protected Workspace(ProjectReaderSettings settings, bool designTime)
        {
            _settings = settings;
            _lockFileReader = new LockFileFormat();
            _designTime = designTime;
        }

        public ProjectContext GetProjectContext(string projectPath, NuGetFramework framework)
        {
            var contexts = GetProjectContextCollection(projectPath);
            if (contexts == null)
            {
                return null;
            }

            return contexts
                .ProjectContexts
                .FirstOrDefault(c => Equals(c.TargetFramework, framework) && string.IsNullOrEmpty(c.RuntimeIdentifier));
        }

        public ProjectContextCollection GetProjectContextCollection(string projectPath, bool clearCache)
        {
            var normalizedPath = ProjectPathHelper.NormalizeProjectDirectoryPath(projectPath);
            if (normalizedPath == null)
            {
                return null;
            }

            if (clearCache)
            {
                _projectContextsCache.Clear();
                _projectsCache.Clear();
                _lockFileCache.Clear();
            }

            return _projectContextsCache.AddOrUpdate(
                normalizedPath,
                key => AddProjectContextEntry(key, null),
                (key, oldEntry) => AddProjectContextEntry(key, oldEntry));
        }

        public ProjectContextCollection GetProjectContextCollection(string projectPath)
        {
            return GetProjectContextCollection(projectPath, clearCache: false);
        }

        public Project GetProject(string projectDirectory) => GetProjectCore(projectDirectory)?.Model;

        private LockFile GetLockFile(string projectDirectory)
        {
            var normalizedPath = ProjectPathHelper.NormalizeProjectDirectoryPath(projectDirectory);
            if (normalizedPath == null)
            {
                return null;
            }

            return _lockFileCache.AddOrUpdate(
                normalizedPath,
                key => AddLockFileEntry(key, null),
                (key, oldEntry) => AddLockFileEntry(key, oldEntry)).Model;
        }


        private FileModelEntry<Project> GetProjectCore(string projectDirectory)
        {
            var normalizedPath = ProjectPathHelper.NormalizeProjectDirectoryPath(projectDirectory);
            if (normalizedPath == null)
            {
                return null;
            }

            return _projectsCache.AddOrUpdate(
                normalizedPath,
                key => AddProjectEntry(key, null),
                (key, oldEntry) => AddProjectEntry(key, oldEntry));
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
                if (!ProjectReader.TryGetProject(projectDirectory, out project, _settings))
                {
                    currentEntry.Reset();
                }
                else
                {
                    currentEntry.Diagnostics.AddRange(project.Diagnostics);
                    currentEntry.Model = project;
                    currentEntry.FilePath = project.ProjectFilePath;
                    currentEntry.UpdateLastWriteTimeUtc();
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

                if (!File.Exists(Path.Combine(projectDirectory, LockFileFormat.LockFileName)))
                {
                    return currentEntry;
                }
                else
                {
                    currentEntry.FilePath = Path.Combine(projectDirectory, LockFileFormat.LockFileName);

                    using (var fs = ResilientFileStreamOpener.OpenFile(currentEntry.FilePath, retry: 2))
                    {
                        try
                        {
                            currentEntry.Model = _lockFileReader.Read(fs, currentEntry.FilePath);
                            currentEntry.UpdateLastWriteTimeUtc();
                        }
                        catch (FileFormatException ex)
                        {
                            throw ex.WithFilePath(currentEntry.FilePath);
                        }
                        catch (Exception ex)
                        {
                            throw FileFormatException.Create(ex, currentEntry.FilePath);
                        }
                    }
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

            var projectEntry = GetProjectCore(projectDirectory);

            if (projectEntry?.Model == null)
            {
                // project doesn't exist anymore
                currentEntry.Reset();
                return currentEntry;
            }

            var project = projectEntry.Model;
            if (currentEntry.HasChanged)
            {
                currentEntry.Reset();

                var contexts = BuildProjectContexts(project);

                currentEntry.ProjectContexts.AddRange(contexts);

                currentEntry.Project = project;
                currentEntry.ProjectFilePath = project.ProjectFilePath;
                currentEntry.LastProjectFileWriteTimeUtc = File.GetLastWriteTimeUtc(currentEntry.ProjectFilePath);

                var lockFilePath = Path.Combine(project.ProjectDirectory, LockFileFormat.LockFileName);
                if (File.Exists(lockFilePath))
                {
                    currentEntry.LockFilePath = lockFilePath;
                    currentEntry.LastLockFileWriteTimeUtc = File.GetLastWriteTimeUtc(lockFilePath);
                }

                currentEntry.ProjectDiagnostics.AddRange(projectEntry.Diagnostics);
                currentEntry.ProjectDiagnostics.AddRange(
                    currentEntry.ProjectContexts.SelectMany(c => c.Diagnostics));
            }

            return currentEntry;
        }

        protected abstract IEnumerable<ProjectContext> BuildProjectContexts(Project project);

        /// <summary>
        /// Creates a ProjectContextBuilder configured to use the Workspace caches.
        /// </summary>
        /// <returns></returns>
        protected ProjectContextBuilder CreateBaseProjectBuilder()
        {
            return new ProjectContextBuilder()
                .WithProjectReaderSettings(_settings)
                .WithProjectResolver(path => GetProjectCore(path)?.Model)
                .WithLockFileResolver(path => GetLockFile(path));
        }

        /// <summary>
        /// Creates a ProjectContextBuilder configured to use the Workspace caches, and the specified root project.
        /// </summary>
        /// <param name="root">The root project</param>
        /// <returns></returns>
        protected ProjectContextBuilder CreateBaseProjectBuilder(Project root)
        {
            return CreateBaseProjectBuilder().WithProject(root);
        }

        protected class FileModelEntry<TModel> where TModel : class
        {
            private DateTime _lastWriteTimeUtc;

            public TModel Model { get; set; }

            public string FilePath { get; set; }

            public List<DiagnosticMessage> Diagnostics { get; } = new List<DiagnosticMessage>();

            public void UpdateLastWriteTimeUtc()
            {
                _lastWriteTimeUtc = File.GetLastWriteTimeUtc(FilePath);
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

                    return _lastWriteTimeUtc < File.GetLastWriteTimeUtc(FilePath);
                }
            }

            public void Reset()
            {
                Model = null;
                FilePath = null;
                Diagnostics.Clear();
                _lastWriteTimeUtc = DateTime.MinValue;
            }
        }

    }
}
