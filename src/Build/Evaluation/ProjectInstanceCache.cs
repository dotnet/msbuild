// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// A process-wide cache of evaluated <see cref="ProjectInstance"/> objects for server mode.
    /// Cached instances are keyed by (projectFullPath, globalProperties) and validated by checking
    /// timestamps of all files in the import chain (MSBuildAllProjects).
    ///
    /// This follows the same pattern as <see cref="ProjectRootElementCache"/>: cached entries
    /// are invalidated when any file that contributed to evaluation has changed on disk.
    /// Unlike ProjectRootElementCache (which caches parsed XML), this caches the fully
    /// evaluated state — properties, items, imports, and targets.
    /// </summary>
    internal static class ProjectInstanceCache
    {
        private static readonly ConcurrentDictionary<ProjectInstanceCacheKey, ProjectInstanceCacheEntry> s_cache = new();

        /// <summary>
        /// Returns a cached <see cref="ProjectInstance"/> if one exists and is still valid
        /// (all imported files have the same timestamp as when the instance was cached).
        /// Returns null on cache miss or if the cached entry is stale.
        /// </summary>
        public static ProjectInstance? TryGet(string projectFullPath, IDictionary<string, string> globalProperties)
        {
            var key = new ProjectInstanceCacheKey(projectFullPath, globalProperties);

            if (!s_cache.TryGetValue(key, out ProjectInstanceCacheEntry? entry))
            {
                return null;
            }

            // Validate: check that no imported file has changed since we cached this evaluation.
            if (IsStale(entry))
            {
                s_cache.TryRemove(key, out _);
                return null;
            }

            // Return a deep clone so target execution mutations don't corrupt the cached original.
            return entry.Instance.DeepCopy();
        }

        /// <summary>
        /// Cache a freshly evaluated <see cref="ProjectInstance"/> along with the timestamps
        /// of all files that contributed to its evaluation.
        /// </summary>
        public static void Store(string projectFullPath, IDictionary<string, string> globalProperties, ProjectInstance instance)
        {
            var key = new ProjectInstanceCacheKey(projectFullPath, globalProperties);

            // Capture timestamps of all imported files at cache time.
            Dictionary<string, DateTime> importTimestamps = CaptureImportTimestamps(instance);

            var entry = new ProjectInstanceCacheEntry(instance, importTimestamps);
            s_cache[key] = entry;
        }

        /// <summary>
        /// Clear the entire cache. Called during cleanup or when invalidation is too broad to be selective.
        /// </summary>
        public static void Clear()
        {
            s_cache.Clear();
        }

        /// <summary>
        /// Number of cached entries (for diagnostics/logging).
        /// </summary>
        public static int Count => s_cache.Count;

        /// <summary>
        /// Check if a cache entry is stale by comparing stored timestamps against current disk state.
        /// </summary>
        private static bool IsStale(ProjectInstanceCacheEntry entry)
        {
            foreach (KeyValuePair<string, DateTime> kvp in entry.ImportFileTimestamps)
            {
                DateTime currentTimestamp = NativeMethodsShared.GetLastWriteFileUtcTime(kvp.Key);

                // If the file no longer exists, GetLastWriteFileUtcTime returns DateTime.MinValue.
                // That's different from what we stored, so the entry is stale.
                if (currentTimestamp != kvp.Value)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Capture the last-write timestamps of all files in the project's import chain.
        /// This is the set of files whose modification would invalidate the evaluation.
        /// </summary>
        private static Dictionary<string, DateTime> CaptureImportTimestamps(ProjectInstance instance)
        {
            var timestamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            // The project file itself.
            string projectPath = instance.FullPath;
            if (!string.IsNullOrEmpty(projectPath))
            {
                timestamps[projectPath] = NativeMethodsShared.GetLastWriteFileUtcTime(projectPath);
            }

            // All imported files from MSBuildAllProjects.
            string allProjects = instance.GetPropertyValue("MSBuildAllProjects");
            if (!string.IsNullOrEmpty(allProjects))
            {
                foreach (string importPath in allProjects.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = importPath.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !timestamps.ContainsKey(trimmed))
                    {
                        timestamps[trimmed] = NativeMethodsShared.GetLastWriteFileUtcTime(trimmed);
                    }
                }
            }

            // NuGet assets file — changes here affect resolved references.
            string extensionsPath = instance.GetPropertyValue("MSBuildProjectExtensionsPath");
            if (!string.IsNullOrEmpty(extensionsPath))
            {
                string assetsPath = Path.Combine(extensionsPath, "project.assets.json");
                timestamps[assetsPath] = NativeMethodsShared.GetLastWriteFileUtcTime(assetsPath);
            }

            return timestamps;
        }

        /// <summary>
        /// Cache key: project path + global properties.
        /// Two builds of the same project with different Configuration/Platform are different cache entries.
        /// </summary>
        private readonly struct ProjectInstanceCacheKey : IEquatable<ProjectInstanceCacheKey>
        {
            public string ProjectFullPath { get; }

            public int GlobalPropertiesHash { get; }

            public ProjectInstanceCacheKey(string projectFullPath, IDictionary<string, string> globalProperties)
            {
                ProjectFullPath = Path.GetFullPath(projectFullPath);
                GlobalPropertiesHash = ComputeGlobalPropertiesHash(globalProperties);
            }

            public bool Equals(ProjectInstanceCacheKey other) =>
                string.Equals(ProjectFullPath, other.ProjectFullPath, StringComparison.OrdinalIgnoreCase)
                && GlobalPropertiesHash == other.GlobalPropertiesHash;

            public override bool Equals(object? obj) => obj is ProjectInstanceCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(ProjectFullPath);
                return (hash * 397) ^ GlobalPropertiesHash;
            }

            private static int ComputeGlobalPropertiesHash(IDictionary<string, string> properties)
            {
                if (properties == null || properties.Count == 0)
                {
                    return 0;
                }

                int hash = 0;

                foreach (KeyValuePair<string, string> kvp in properties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(kvp.Key);
                    hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(kvp.Value ?? string.Empty);
                }

                return hash;
            }
        }

        /// <summary>
        /// Cache entry: the evaluated ProjectInstance plus the timestamps of all files
        /// that contributed to the evaluation.
        /// </summary>
        private sealed class ProjectInstanceCacheEntry
        {
            public ProjectInstance Instance { get; }

            /// <summary>
            /// Map of file path → LastWriteTimeUtc at the time of evaluation.
            /// Includes the project file, all imports, and the NuGet assets file.
            /// </summary>
            public Dictionary<string, DateTime> ImportFileTimestamps { get; }

            public ProjectInstanceCacheEntry(ProjectInstance instance, Dictionary<string, DateTime> importFileTimestamps)
            {
                Instance = instance;
                ImportFileTimestamps = importFileTimestamps;
            }
        }
    }
}
