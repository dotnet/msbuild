// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents the on-disk serialization format of the RAR cache.
    /// </summary>
    internal class ResolveAssemblyReferenceCache : StateFileBase, ITranslatable
    {
        /// <summary>
        /// Cache at the ResolveAssemblyReferenceCache instance level. It is serialized and reused between instances.
        /// </summary>
        internal Dictionary<string, FileState> instanceLocalFileStateCache = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// True if the contents have changed.
        /// </summary>
        protected bool isDirty;

        /// <summary>
        /// Flag that indicates that <see cref="instanceLocalFileStateCache"/> has been modified.
        /// </summary>
        /// <value></value>
        internal bool IsDirty
        {
            get { return isDirty; }
            set { isDirty = value; }
        }

        /// <summary>
        /// Class that holds the current file state.
        /// </summary>
        internal record class FileState : ITranslatable
        {
            /// <summary>
            /// The last modified time for this file.
            /// </summary>
            private DateTime lastModified;

            /// <summary>
            /// The fusion name of this file.
            /// </summary>
            private AssemblyNameExtension assemblyName;

            /// <summary>
            /// The assemblies that this file depends on.
            /// </summary>
            internal AssemblyNameExtension[] dependencies;

            /// <summary>
            /// The scatter files associated with this assembly.
            /// </summary>
            internal string[] scatterFiles;

            /// <summary>
            /// FrameworkName the file was built against
            /// </summary>
            internal FrameworkName frameworkName;

            /// <summary>
            /// The CLR runtime version for the assembly.
            /// </summary>
            internal string runtimeVersion;

            /// <summary>
            /// Default construct.
            /// </summary>
            internal FileState(DateTime lastModified)
            {
                this.lastModified = lastModified;
            }

            /// <summary>
            /// Ctor for translator deserialization
            /// </summary>
            internal FileState(ITranslator translator)
            {
                Translate(translator);
            }

            /// <summary>
            /// Reads/writes this class
            /// </summary>
            public void Translate(ITranslator translator)
            {
                ErrorUtilities.VerifyThrowArgumentNull(translator, nameof(translator));

                translator.Translate(ref lastModified);
                translator.Translate(ref assemblyName,
                    (ITranslator t) => new AssemblyNameExtension(t));
                translator.TranslateArray(ref dependencies,
                    (ITranslator t) => new AssemblyNameExtension(t));
                translator.Translate(ref scatterFiles);
                translator.Translate(ref runtimeVersion);
                translator.Translate(ref frameworkName);
            }

            /// <summary>
            /// Gets the last modified date.
            /// </summary>
            /// <value></value>
            internal DateTime LastModified
            {
                get { return lastModified; }
            }

            /// <summary>
            /// Get or set the assemblyName.
            /// </summary>
            /// <value></value>
            internal AssemblyNameExtension Assembly
            {
                get { return assemblyName; }
                set { assemblyName = value; }
            }

            /// <summary>
            /// Get or set the runtimeVersion
            /// </summary>
            /// <value></value>
            internal string RuntimeVersion
            {
                get { return runtimeVersion; }
                set { runtimeVersion = value; }
            }

            /// <summary>
            /// Get or set the framework name the file was built against
            /// </summary>
            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Could be used in other assemblies")]
            internal FrameworkName FrameworkNameAttribute
            {
                get { return frameworkName; }
                set { frameworkName = value; }
            }

            /// <summary>
            /// The last-modified value to use for immutable framework files which we don't do I/O on.
            /// </summary>
            internal static DateTime ImmutableFileLastModifiedMarker => DateTime.MaxValue;

            /// <summary>
            /// It is wasteful to persist entries for immutable framework files.
            /// </summary>
            internal bool IsWorthPersisting => lastModified != ImmutableFileLastModifiedMarker;
        }

        public ResolveAssemblyReferenceCache()
        {
        }

        public ResolveAssemblyReferenceCache(ITranslator translator)
        {
            Translate(translator);
        }

        protected ResolveAssemblyReferenceCache(ResolveAssemblyReferenceCache anotherCache)
        {
            if (anotherCache != null)
            {
                instanceLocalFileStateCache = anotherCache.instanceLocalFileStateCache;
                isDirty = anotherCache.isDirty;
            }
        }

        /// <summary>
        /// Reads/writes this class.
        /// Used for serialization and deserialization of this class persistent cache.
        /// </summary>
        public override void Translate(ITranslator translator)
        {
            if (instanceLocalFileStateCache is null)
            {
                throw new NullReferenceException(nameof(instanceLocalFileStateCache));
            }

            translator.TranslateDictionary(
                ref instanceLocalFileStateCache,
                StringComparer.OrdinalIgnoreCase,
                (ITranslator t) => new FileState(t));

            // IsDirty should be false for either direction. Either this cache was brought
            // up-to-date with the on-disk cache or vice versa. Either way, they agree.
            IsDirty = false;
        }

        /// <summary>
        /// Reads in cached data from stateFiles to build an initial cache. Avoids logging warnings or errors.
        /// </summary>
        /// <param name="stateFiles">List of locations of caches on disk.</param>
        /// <param name="log">How to log</param>
        /// <param name="fileExists">Whether a file exists</param>
        /// <returns>A cache representing key aspects of file states.</returns>
        internal static ResolveAssemblyReferenceCache DeserializePrecomputedCaches(ITaskItem[] stateFiles, TaskLoggingHelper log, FileExists fileExists)
        {
            ResolveAssemblyReferenceCache retVal = new ResolveAssemblyReferenceCache();
            retVal.isDirty = stateFiles.Length > 0;
            HashSet<string> assembliesFound = new HashSet<string>();

            foreach (ITaskItem stateFile in stateFiles)
            {
                // Verify that it's a real stateFile. Log message but do not error if not.
                ResolveAssemblyReferenceCache cache = DeserializeCache<ResolveAssemblyReferenceCache>(stateFile.ToString(), log);
                if (cache == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, FileState> kvp in cache.instanceLocalFileStateCache)
                {
                    string relativePath = kvp.Key;
                    if (!assembliesFound.Contains(relativePath))
                    {
                        FileState fileState = kvp.Value;
                        string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(stateFile.ToString()), relativePath));
                        if (fileExists(fullPath))
                        {
                            // Correct file path
                            retVal.instanceLocalFileStateCache[fullPath] = fileState;
                            assembliesFound.Add(relativePath);
                        }
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Modifies this object to be more portable across machines, then writes it to filePath.
        /// </summary>
        /// <param name="stateFile">Path to which to write the precomputed cache</param>
        /// <param name="log">How to log</param>
        internal void SerializePrecomputedCache(string stateFile, TaskLoggingHelper log)
        {
            // Save a copy of instanceLocalFileStateCache so we can restore it later. SerializeCacheByTranslator serializes
            // instanceLocalFileStateCache by default, so change that to the relativized form, then change it back.
            Dictionary<string, FileState> oldFileStateCache = instanceLocalFileStateCache;
            instanceLocalFileStateCache = instanceLocalFileStateCache.ToDictionary(kvp => FileUtilities.MakeRelative(Path.GetDirectoryName(stateFile), kvp.Key), kvp => kvp.Value);

            try
            {
                if (FileUtilities.FileExistsNoThrow(stateFile))
                {
                    log.LogWarningWithCodeFromResources("General.StateFileAlreadyPresent", stateFile);
                }
                SerializeCache(stateFile, log);
            }
            finally
            {
                instanceLocalFileStateCache = oldFileStateCache;
            }
        }

        /// <summary>
        /// Merges the existing data in <paramref name="toCache"/> the data from <paramref name="fromCache"/> and sets <see cref="IsDirty"/>
        /// on <paramref name="toCache"/> accordingly.
        /// </summary>
        /// <param name="fromCache">The cache deserialized from disk.</param>
        /// <param name="toCache">The cache built so far during the current RAR task execution.</param>
        internal static void MergeInstanceLocalFileStateCache(ResolveAssemblyReferenceCache fromCache, ResolveAssemblyReferenceCache toCache)
        {
            // Special case: toCache is empty.
            if (toCache.instanceLocalFileStateCache.Count == 0)
            {
                toCache.instanceLocalFileStateCache = fromCache.instanceLocalFileStateCache;
                toCache.IsDirty = false;
            }
            else
            {
                // If "to" is bigger than "from", then mark it dirty because we will want to save back the extras.
                bool toIsDirty = toCache.instanceLocalFileStateCache.Count > fromCache.instanceLocalFileStateCache.Count;

                foreach (KeyValuePair<string, FileState> kvp in fromCache.instanceLocalFileStateCache)
                {
                    // The "to" FileState is more up-to-date, so we add missing items only. We compare items present in both dictionaries
                    // to calculate the new value of toCache.IsDirty.
                    if (toCache.instanceLocalFileStateCache.TryGetValue(kvp.Key, out FileState toFileState))
                    {
                        toIsDirty |= !toFileState.Equals(kvp.Value);
                    }
                    else
                    {
                        toCache.instanceLocalFileStateCache.Add(kvp.Key, kvp.Value);
                    }
                }

                toCache.IsDirty = toIsDirty;
            }
        }
    }
}
