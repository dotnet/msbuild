using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Tasks.ResolveAssemblyReferences.Abstractions;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal class ResolveAssemblyReferenceCacheService : IResolveAssemblyReferenceService
    {
        private IResolveAssemblyReferenceService RarService { get; }

        private IDirectoryWatcher Watcher { get; }

        private Dictionary<string, ResolveAssemblyReferenceEvaluation> EvaluationCache { get; } =
            new Dictionary<string, ResolveAssemblyReferenceEvaluation>(StringComparer.Ordinal);

        private Dictionary<string, HashSet<string>> DirectoryToWatchingProjects { get; } =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, HashSet<string>> FileToWatchingProjects { get; } =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private object CacheLock { get; } = new object();

        internal ResolveAssemblyReferenceCacheService(
            IResolveAssemblyReferenceService rarService,
            IDirectoryWatcher watcher
        )
        {
            RarService = rarService;
            Watcher = watcher;
        }

        public ResolveAssemblyReferenceResponse ResolveAssemblyReferences
        (
            ResolveAssemblyReferenceRequest req
        )
        {
            // TODO: Determine proper identifier for a project, StateFile path was just the most obvious unique property
            string projectId = req.StateFile;

            // TODO: Better concurrency model, needs more investigation on where concurrent data structures can be
            // utilized and where it is safe to remove locks as all three dictionaries are currently updated as
            // an atomic operation.
            lock (CacheLock)
            {
                InvalidateOutOfDateEvaluations();

                bool isEvaluationCached =
                    EvaluationCache.TryGetValue(projectId, out ResolveAssemblyReferenceEvaluation cachedEvaluation);

                bool isSameInput = isEvaluationCached
                                   && ResolveAssemblyReferenceRequestComparer.Equals(req, cachedEvaluation.Request);

                if (isSameInput)
                {
                    return cachedEvaluation.Response;
                }

                EvaluationCache.Remove(projectId);
            }

            ResolveAssemblyReferenceResponse resp = RarService.ResolveAssemblyReferences(req);

            lock (CacheLock)
            {
                WatchTrackedPaths(projectId, resp);

                var evaluation = new ResolveAssemblyReferenceEvaluation(req, resp);
                EvaluationCache[projectId] = evaluation;
            }

            return resp;
        }

        private void InvalidateOutOfDateEvaluations()
        {
            foreach (FileSystemChange fileSystemChange in Watcher.RecentChanges)
            {
                InvalidateEvaluationsForProjects(DirectoryToWatchingProjects, fileSystemChange.Directory);
                InvalidateEvaluationsForProjects(FileToWatchingProjects, fileSystemChange.File);
            }
        }

        private void InvalidateEvaluationsForProjects(Dictionary<string, HashSet<string>> pathToWatchingProjects, string path)
        {
            if (pathToWatchingProjects.TryGetValue(path, out HashSet<string> projects))
            {
                foreach (string projectId in projects)
                {
                    EvaluationCache.Remove(projectId);
                }

                projects.Clear();
            }
        }

        private void WatchTrackedPaths(string projectId, ResolveAssemblyReferenceResponse resp)
        {
            foreach (string directory in resp.TrackedDirectories)
            {
                WatchDirectory(projectId, directory);
            }
            foreach (string file in resp.TrackedFiles)
            {
                WatchFile(projectId, file);
            }
        }

        private void WatchDirectory(string projectId, string directory)
        {
            Watcher.Watch(directory);

            if (!DirectoryToWatchingProjects.TryGetValue(directory, out HashSet<string> watchingProjects))
            {
                watchingProjects = new HashSet<string>(StringComparer.Ordinal);
                DirectoryToWatchingProjects.Add(directory, watchingProjects);
            }

            watchingProjects.Add(projectId);
        }

        private void WatchFile(string projectId, string file)
        {
            string directory = Path.GetDirectoryName(file);
            Watcher.Watch(directory);

            if (!FileToWatchingProjects.TryGetValue(file, out HashSet<string> watchingProjects))
            {
                watchingProjects = new HashSet<string>(StringComparer.Ordinal);
                FileToWatchingProjects.Add(file, watchingProjects);
            }

            watchingProjects.Add(projectId);
        }
    }
}
