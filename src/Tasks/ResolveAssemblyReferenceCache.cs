// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
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
        /// 
        /// </summary>
        internal static ConcurrentDictionary<string, (DateTime FileTimestamp, long ContentSequenceNumber)> s_processWideCacheFileCache = new(StringComparer.OrdinalIgnoreCase);

        private static long s_sequenceNumber;

        internal static long GetNextSequenceNumber() => Interlocked.Increment(ref s_sequenceNumber);

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
        /// True if we this cache is empty.
        /// </summary>
        internal bool IsInstanceLocalCacheEmpty => instanceLocalFileStateCache.Count == 0;

        /// <summary>
        /// Class that holds the current file state.
        /// </summary>
        internal sealed class FileState : ITranslatable, IEquatable<FileState>
        {
            /// <summary>
            /// The value of a monotonically increasing counter at the time this instance was last modified. Not to be serialized.
            /// </summary>
            private long sequenceNumber;

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
            private AssemblyNameExtension[] dependencies;

            /// <summary>
            /// The scatter files associated with this assembly.
            /// </summary>
            private string[] scatterFiles;

            /// <summary>
            /// FrameworkName the file was built against
            /// </summary>
            private FrameworkName frameworkName;

            /// <summary>
            /// The CLR runtime version for the assembly.
            /// </summary>
            private string runtimeVersion;

            /// <summary>
            /// Default construct.
            /// </summary>
            internal FileState(DateTime lastModified)
            {
                sequenceNumber = GetNextSequenceNumber();
                this.lastModified = lastModified;
            }

            /// <summary>
            /// Ctor for translator deserialization
            /// </summary>
            internal FileState(ITranslator translator)
            {
                sequenceNumber = GetNextSequenceNumber();
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

            public bool Equals(FileState other)
            {
                bool NullAwareSequenceEquals<T>(IEnumerable<T> first, IEnumerable<T> second)
                {
                    if (first == null || second == null)
                    {
                        return first == second;
                    }
                    return Enumerable.SequenceEqual(first, second);
                }

                bool NullAwareEquatableEquals<T>(IEquatable<T> first, IEquatable<T> second) where T : class
                {
                    if (first == null || second == null)
                    {
                        return first == second;
                    }
                    return first.Equals(second as T);
                }

                return
                    lastModified == other.LastModified &&
                    NullAwareEquatableEquals(assemblyName, other.assemblyName) &&
                    NullAwareSequenceEquals(dependencies, other.dependencies) &&
                    NullAwareSequenceEquals(scatterFiles, other.scatterFiles) &&
                    NullAwareEquatableEquals(frameworkName, other.frameworkName) &&
                    runtimeVersion == other.runtimeVersion;
            }

            public void MergeTo(FileState other)
            {
                // If we're not talking about the same version of the assembly then don't do anything.
                if (lastModified == other.lastModified)
                {
                    if (assemblyName != null && other.assemblyName == null)
                    {
                        other.assemblyName = assemblyName;
                    }
                    if (dependencies != null && other.dependencies == null)
                    {
                        other.dependencies = dependencies;
                    }
                    if (scatterFiles != null && other.scatterFiles == null)
                    {
                        other.scatterFiles = scatterFiles;
                    }
                    if (frameworkName != null && other.frameworkName == null)
                    {
                        other.frameworkName = frameworkName;
                    }
                    if (runtimeVersion != null && other.runtimeVersion == null)
                    {
                        other.runtimeVersion = runtimeVersion;
                    }
                }
            }

            internal long SequenceNumber => sequenceNumber;

            /// <summary>
            /// Gets the last modified date.
            /// </summary>
            /// <value></value>
            internal DateTime LastModified => lastModified;

            /// <summary>
            /// Get or set the assemblyName.
            /// </summary>
            /// <value></value>
            internal AssemblyNameExtension Assembly
            {
                get => assemblyName;
                set
                {
                    assemblyName = value;
                    sequenceNumber = GetNextSequenceNumber();
                }
            }

            /// <summary>
            /// 
            /// </summary>
            internal AssemblyNameExtension[] Dependencies
            {
                get => dependencies;
                set
                {
                    dependencies = value;
                    sequenceNumber = GetNextSequenceNumber();
                }
            }

            /// <summary>
            /// 
            /// </summary>
            internal string[] ScatterFiles
            {
                get => scatterFiles;
                set
                {
                    scatterFiles = value;
                    sequenceNumber = GetNextSequenceNumber();
                }
            }

            /// <summary>
            /// 
            /// </summary>
            internal FrameworkName FrameworkName
            {
                get => frameworkName;
                set
                {
                    frameworkName = value;
                    sequenceNumber = GetNextSequenceNumber();
                }
            }

            /// <summary>
            /// Get or set the runtimeVersion
            /// </summary>
            /// <value></value>
            internal string RuntimeVersion
            {
                get => runtimeVersion;
                set
                {
                    runtimeVersion = value;
                    sequenceNumber = GetNextSequenceNumber();
                }
            }

            /// <summary>
            /// Get or set the framework name the file was built against
            /// </summary>
            internal FrameworkName FrameworkNameAttribute
            {
                get => frameworkName;
                set
                {
                    frameworkName = value;
                    sequenceNumber = GetNextSequenceNumber();
                }
            }

            /// <summary>
            /// 
            /// </summary>
            internal void SetAssemblyMetadata(AssemblyNameExtension[] dependencies, string[] scatterFiles, FrameworkName frameworkName)
            {
                this.dependencies = dependencies;
                this.scatterFiles = scatterFiles;
                this.frameworkName = frameworkName;
                sequenceNumber = GetNextSequenceNumber();
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
        /// Merges the existing data in <paramref name="toCache"/> with the data from <paramref name="fromCache"/> and sets <see cref="IsDirty"/>
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
                    // We set toCache.IsDirty if the "to" FileState ends up being different from the "from" one as this indicates
                    // the need to write the updated cache back to disk.
                    if (toCache.instanceLocalFileStateCache.TryGetValue(kvp.Key, out FileState toFileState))
                    {
                        kvp.Value.MergeTo(toFileState);
                        toIsDirty = toIsDirty || !toFileState.Equals(kvp.Value);
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
