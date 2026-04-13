// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// A process-wide cache of <see cref="ProjectGraph"/> objects for server mode.
    /// The cached graph is validated by checking timestamps of the solution file
    /// and all project files in the graph. If any file changed, the graph is
    /// invalidated and must be reconstructed.
    ///
    /// Graph construction is expensive because it evaluates every reachable project.
    /// When combined with <see cref="Evaluation.ProjectInstanceCache"/>, cache misses
    /// on the graph level can still reuse cached evaluations, making reconstruction cheaper.
    /// </summary>
    internal static class ProjectGraphCache
    {
        private static ProjectGraphCacheEntry? s_cachedEntry;
        private static readonly object s_lock = new();

        /// <summary>
        /// Returns a cached <see cref="ProjectGraph"/> if one exists and is still valid.
        /// Returns null on cache miss or if the cached graph is stale.
        /// </summary>
        public static ProjectGraph? TryGet(IEnumerable<ProjectGraphEntryPoint> entryPoints)
        {
            lock (s_lock)
            {
                if (s_cachedEntry is null)
                {
                    return null;
                }

                // Check that entry points match.
                if (!EntryPointsMatch(entryPoints, s_cachedEntry.EntryPoints))
                {
                    s_cachedEntry = null;
                    return null;
                }

                // Check that no project file in the graph has changed.
                if (IsStale(s_cachedEntry))
                {
                    s_cachedEntry = null;
                    return null;
                }

                return s_cachedEntry.Graph;
            }
        }

        /// <summary>
        /// Cache a freshly constructed <see cref="ProjectGraph"/>.
        /// </summary>
        public static void Store(IEnumerable<ProjectGraphEntryPoint> entryPoints, ProjectGraph graph)
        {
            Dictionary<string, DateTime> fileTimestamps = CaptureGraphFileTimestamps(graph);

            lock (s_lock)
            {
                s_cachedEntry = new ProjectGraphCacheEntry(entryPoints, graph, fileTimestamps);
            }
        }

        /// <summary>
        /// Clear the cached graph.
        /// </summary>
        public static void Clear()
        {
            lock (s_lock)
            {
                s_cachedEntry = null;
            }
        }

        private static bool EntryPointsMatch(
            IEnumerable<ProjectGraphEntryPoint> a,
            IReadOnlyList<ProjectGraphEntryPoint> b)
        {
            int i = 0;
            foreach (var ep in a)
            {
                if (i >= b.Count)
                {
                    return false;
                }

                if (!string.Equals(ep.ProjectFile, b[i].ProjectFile, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                i++;
            }

            return i == b.Count;
        }

        private static bool IsStale(ProjectGraphCacheEntry entry)
        {
            foreach (KeyValuePair<string, DateTime> kvp in entry.FileTimestamps)
            {
                DateTime currentTimestamp = NativeMethodsShared.GetLastWriteFileUtcTime(kvp.Key);
                if (currentTimestamp != kvp.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, DateTime> CaptureGraphFileTimestamps(ProjectGraph graph)
        {
            var timestamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            foreach (ProjectGraphNode node in graph.ProjectNodes)
            {
                string path = node.ProjectInstance.FullPath;
                if (!string.IsNullOrEmpty(path) && !timestamps.ContainsKey(path))
                {
                    timestamps[path] = NativeMethodsShared.GetLastWriteFileUtcTime(path);
                }
            }

            return timestamps;
        }

        private sealed class ProjectGraphCacheEntry
        {
            public IReadOnlyList<ProjectGraphEntryPoint> EntryPoints { get; }

            public ProjectGraph Graph { get; }

            public Dictionary<string, DateTime> FileTimestamps { get; }

            public ProjectGraphCacheEntry(
                IEnumerable<ProjectGraphEntryPoint> entryPoints,
                ProjectGraph graph,
                Dictionary<string, DateTime> fileTimestamps)
            {
                EntryPoints = entryPoints.ToList();
                Graph = graph;
                FileTimestamps = fileTimestamps;
            }
        }
    }
}
