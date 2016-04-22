// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Frameworks;

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
        private readonly ProjectReaderSettings _settings;
        private readonly bool _designTime;
        private readonly LockFileReader _lockFileReader;

        private WorkspaceContext(IEnumerable<string> projectPaths, ProjectReaderSettings settings, bool designTime)
        {
            _settings = settings;
            _designTime = designTime;
            _lockFileReader = new LockFileReader();

            foreach (var path in projectPaths)
            {
                AddProject(path);
            }

            Refresh();
        }

        /// <summary>
        /// Create a WorkspaceContext from a given path.
        ///
        /// If the given path points to a global.json, all the projects found under the search paths
        /// are added to the WorkspaceContext.
        ///
        /// If the given path points to a project.json, all the projects it referenced as well as itself
        /// are added to the WorkspaceContext.
        ///
        /// If no path is provided, the workspace context is created empty and projects must be manually added
        /// to it using <see cref="AddProject(string)"/>.
        /// </summary>
        public static WorkspaceContext CreateFrom(string projectPath, bool designTime)
        {
            var projectPaths = ResolveProjectPath(projectPath);
            if (projectPaths == null || !projectPaths.Any())
            {
                return new WorkspaceContext(Enumerable.Empty<string>(), ProjectReaderSettings.ReadFromEnvironment(), designTime);
            }

            var context = new WorkspaceContext(projectPaths, ProjectReaderSettings.ReadFromEnvironment(), designTime);
            return context;
        }

        /// <summary>
        /// Create an empty <see cref="WorkspaceContext" /> using the default <see cref="ProjectReaderSettings" />
        /// </summary>
        /// <param name="designTime">A boolean indicating if the workspace should be created in Design-Time mode</param>
        /// <returns></returns>
        public static WorkspaceContext Create(bool designTime) => Create(ProjectReaderSettings.ReadFromEnvironment(), designTime);

        /// <summary>
        /// Create an empty <see cref="WorkspaceContext" /> using the default <see cref="ProjectReaderSettings" />, with the specified Version Suffix
        /// </summary>
        /// <param name="versionSuffix">The suffix to use to replace any '-*' snapshot tokens in Project versions.</param>
        /// <param name="designTime">A boolean indicating if the workspace should be created in Design-Time mode</param>
        /// <returns></returns>
        public static WorkspaceContext Create(string versionSuffix, bool designTime)
        {
            var settings = ProjectReaderSettings.ReadFromEnvironment();
            if (!string.IsNullOrEmpty(versionSuffix))
            {
                settings.VersionSuffix = versionSuffix;
            }
            return Create(settings, designTime);
        }

        /// <summary>
        /// Create an empty <see cref="WorkspaceContext" /> using the provided <see cref="ProjectReaderSettings" />.
        /// </summary>
        /// <param name="settings">The settings to use when reading projects</param>
        /// <param name="designTime">A boolean indicating if the workspace should be created in Design-Time mode</param>
        /// <returns></returns>
        public static WorkspaceContext Create(ProjectReaderSettings settings, bool designTime)
        {
            return new WorkspaceContext(Enumerable.Empty<string>(), settings, designTime);
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
                var project = GetProjectCore(projectDirectory).Model;
                if (project == null)
                {
                    continue;
                }

                _projects.Add(project.ProjectDirectory);

                foreach (var projectContext in GetProjectContexts(project.ProjectDirectory))
                {
                    foreach (var reference in GetProjectReferences(projectContext))
                    {
                        var referencedProject = GetProjectCore(reference.Path).Model;
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
            return (IReadOnlyList<ProjectContext>)GetProjectContextCollection(projectPath)?.ProjectContexts.AsReadOnly() ??
                EmptyArray<ProjectContext>.Value;
        }

        public ProjectContext GetProjectContext(string projectPath, NuGetFramework framework) => GetProjectContext(projectPath, framework, EmptyArray<string>.Value);

        public ProjectContext GetProjectContext(string projectPath, NuGetFramework framework, IEnumerable<string> runtimeIdentifier)
        {
            var contexts = GetProjectContextCollection(projectPath);
            if (contexts == null)
            {
                return null;
            }

            return contexts
                .ProjectContexts
                .FirstOrDefault(c => Equals(c.TargetFramework, framework) && RidsMatch(c.RuntimeIdentifier, runtimeIdentifier));
        }

        public ProjectContext GetRuntimeContext(ProjectContext context, IEnumerable<string> runtimeIdentifiers)
        {
            var contexts = GetProjectContextCollection(context.ProjectDirectory);
            if (contexts == null)
            {
                return null;
            }

            var runtimeContext = runtimeIdentifiers
                .Select(r => contexts.GetTarget(context.TargetFramework, r))
                .FirstOrDefault(c => c != null);

            if (runtimeContext == null)
            {
                if (context.IsPortable)
                {
                    // We're specializing a portable target, so synthesize a runtime target manually
                    // We don't cache this project context, but we'll still use the cached Project and LockFile
                    return InitializeProjectContextBuilder(context.ProjectFile)
                        .WithTargetFramework(context.TargetFramework)
                        .WithRuntimeIdentifiers(runtimeIdentifiers)
                        .Build();
                }

                // We are standalone, but don't support this runtime
                var rids = string.Join(", ", runtimeIdentifiers);
                throw new InvalidOperationException($"Can not find runtime target for framework '{context.TargetFramework}' compatible with one of the target runtimes: '{rids}'. " +
                                                    "Possible causes:" + Environment.NewLine +
                                                    "1. The project has not been restored or restore failed - run `dotnet restore`" + Environment.NewLine +
                                                    $"2. The project does not list one of '{rids}' in the 'runtimes' section.");
            }

            return runtimeContext;
        }

        public Project GetProject(string projectDirectory) => GetProjectCore(projectDirectory)?.Model;

        public ProjectContextCollection GetProjectContextCollection(string projectPath)
        {
            var normalizedPath = NormalizeProjectPath(projectPath);
            if (normalizedPath == null)
            {
                return null;
            }

            return _projectContextsCache.AddOrUpdate(
                normalizedPath,
                key => AddProjectContextEntry(key, null),
                (key, oldEntry) => AddProjectContextEntry(key, oldEntry));
        }

        private FileModelEntry<Project> GetProjectCore(string projectDirectory)
        {
            var normalizedPath = NormalizeProjectPath(projectDirectory);
            if (normalizedPath == null)
            {
                return null;
            }

            return _projectsCache.AddOrUpdate(
                normalizedPath,
                key => AddProjectEntry(key, null),
                (key, oldEntry) => AddProjectEntry(key, oldEntry));
        }

        private LockFile GetLockFile(string projectDirectory)
        {
            var normalizedPath = NormalizeProjectPath(projectDirectory);
            if (normalizedPath == null)
            {
                return null;
            }

            return _lockFileCache.AddOrUpdate(
                normalizedPath,
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
                if (!ProjectReader.TryGetProject(projectDirectory, out project, currentEntry.Diagnostics, _settings))
                {
                    currentEntry.Reset();
                }
                else
                {
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

                if (!File.Exists(Path.Combine(projectDirectory, LockFile.FileName)))
                {
                    return currentEntry;
                }
                else
                {
                    currentEntry.FilePath = Path.Combine(projectDirectory, LockFile.FileName);

                    using (var fs = ResilientFileStreamOpener.OpenFile(currentEntry.FilePath, retry: 2))
                    {
                        try
                        {
                            currentEntry.Model = _lockFileReader.ReadLockFile(currentEntry.FilePath, fs, designTime: true);
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

                var builder = InitializeProjectContextBuilder(project);

                currentEntry.ProjectContexts.AddRange(builder.BuildAllTargets());

                currentEntry.Project = project;
                currentEntry.ProjectFilePath = project.ProjectFilePath;
                currentEntry.LastProjectFileWriteTimeUtc = File.GetLastWriteTimeUtc(currentEntry.ProjectFilePath);

                var lockFilePath = Path.Combine(project.ProjectDirectory, LockFile.FileName);
                if (File.Exists(lockFilePath))
                {
                    currentEntry.LockFilePath = lockFilePath;
                    currentEntry.LastLockFileWriteTimeUtc = File.GetLastWriteTimeUtc(lockFilePath);
                }

                currentEntry.ProjectDiagnostics.AddRange(projectEntry.Diagnostics);
            }

            return currentEntry;
        }

        private ProjectContextBuilder InitializeProjectContextBuilder(Project project)
        {
            var builder = new ProjectContextBuilder()
                .WithProjectResolver(path => GetProjectCore(path)?.Model)
                .WithLockFileResolver(path => GetLockFile(path))
                .WithProject(project);
            if (_designTime)
            {
                builder.AsDesignTime();
            }

            return builder;
        }

        private class FileModelEntry<TModel> where TModel : class
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

        private static bool RidsMatch(string rid, IEnumerable<string> compatibleRids)
        {
            return (string.IsNullOrEmpty(rid) && !compatibleRids.Any()) ||
                (compatibleRids.Contains(rid));
        }
    }
}
