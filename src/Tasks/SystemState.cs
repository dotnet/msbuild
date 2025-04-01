// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Class is used to cache system state.
    /// </summary>
    internal sealed class SystemState : StateFileBase, ITranslatable
    {
        /// <summary>
        /// Cache at the SystemState instance level. Has the same contents as <see cref="instanceLocalFileStateCache"/>.
        /// It acts as a flag to enforce that an entry has been checked for staleness only once.
        /// </summary>
        private Dictionary<string, FileState> upToDateLocalFileStateCache = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Cache at the SystemState instance level.
        /// </summary>
        /// <remarks>
        /// Before starting execution, RAR attempts to populate this field by deserializing a per-project cache file. During execution,
        /// <see cref="FileState"/> objects that get actually used are inserted into <see cref="instanceLocalOutgoingFileStateCache"/>.
        /// After execution, <see cref="instanceLocalOutgoingFileStateCache"/> is serialized and written to disk if it's different from
        /// what we originally deserialized into this field.
        /// </remarks>
        internal Dictionary<string, FileState> instanceLocalFileStateCache = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Cache at the SystemState instance level. It is serialized to disk and reused between instances via <see cref="instanceLocalFileStateCache"/>.
        /// </summary>
        internal Dictionary<string, FileState> instanceLocalOutgoingFileStateCache = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// LastModified information is purely instance-local. It doesn't make sense to
        /// cache this for long periods of time since there's no way (without actually
        /// calling File.GetLastWriteTimeUtc) to tell whether the cache is out-of-date.
        /// </summary>
        private Dictionary<string, DateTime> instanceLocalLastModifiedCache = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// DirectoryExists information is purely instance-local. It doesn't make sense to
        /// cache this for long periods of time since there's no way (without actually
        /// calling Directory.Exists) to tell whether the cache is out-of-date.
        /// </summary>
        private Dictionary<string, bool> instanceLocalDirectoryExists = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// GetDirectories information is also purely instance-local. This information
        /// is only considered good for the lifetime of the task (or whatever) that owns
        /// this instance.
        /// </summary>
        private Dictionary<string, string[]> instanceLocalDirectories = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Additional level of caching kept at the process level.
        /// </summary>
        private static readonly ConcurrentDictionary<string, FileState> s_processWideFileStateCache = new ConcurrentDictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// XML tables of installed assemblies.
        /// </summary>
        private RedistList redistList;

        /// <summary>
        /// True if the contents have changed.
        /// </summary>
        private bool isDirty;

        /// <summary>
        /// Delegate used internally.
        /// </summary>
        private GetLastWriteTime getLastWriteTime;

        /// <summary>
        /// Cached delegate.
        /// </summary>
        private GetAssemblyName getAssemblyName;

        /// <summary>
        /// Cached delegate.
        /// </summary>
        private GetAssemblyMetadata getAssemblyMetadata;

        /// <summary>
        /// Cached delegate.
        /// </summary>
        private DirectoryExists directoryExists;

        /// <summary>
        /// Cached delegate.
        /// </summary>
        private GetDirectories getDirectories;

        /// <summary>
        /// Cached delegate
        /// </summary>
        private GetAssemblyRuntimeVersion getAssemblyRuntimeVersion;

        /// <summary>
        /// Class that holds the current file state.
        /// </summary>
        internal sealed class FileState : ITranslatable
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
                ErrorUtilities.VerifyThrowArgumentNull(translator);

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

        /// <summary>
        /// Construct.
        /// </summary>
        public SystemState()
        {
        }

        public SystemState(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Set the target framework paths.
        /// This is used to optimize IO in the case of files requested from one
        /// of the FX folders.
        /// </summary>
        /// <param name="installedAssemblyTableInfos">List of Assembly Table Info.</param>
        internal void SetInstalledAssemblyInformation(
            AssemblyTableInfo[] installedAssemblyTableInfos)
        {
            redistList = RedistList.GetRedistList(installedAssemblyTableInfos);
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
                ref (translator.Mode == TranslationDirection.WriteToStream) ? ref instanceLocalOutgoingFileStateCache : ref instanceLocalFileStateCache,
                StringComparer.OrdinalIgnoreCase,
                (ITranslator t) => new FileState(t));

            // IsDirty should be false for either direction. Either this cache was brought
            // up-to-date with the on-disk cache or vice versa. Either way, they agree.
            IsDirty = false;
        }

        /// <inheritdoc />
        internal override bool HasStateToSave => instanceLocalOutgoingFileStateCache.Count > 0;

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
        /// Set the GetLastWriteTime delegate.
        /// </summary>
        /// <param name="getLastWriteTimeValue">Delegate used to get the last write time.</param>
        internal void SetGetLastWriteTime(GetLastWriteTime getLastWriteTimeValue)
        {
            getLastWriteTime = getLastWriteTimeValue;
        }

        /// <summary>
        /// Cache the results of a GetAssemblyName delegate.
        /// </summary>
        /// <param name="getAssemblyNameValue">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal GetAssemblyName CacheDelegate(GetAssemblyName getAssemblyNameValue)
        {
            getAssemblyName = getAssemblyNameValue;
            return GetAssemblyName;
        }

        /// <summary>
        /// Cache the results of a GetAssemblyMetadata delegate.
        /// </summary>
        /// <param name="getAssemblyMetadataValue">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal GetAssemblyMetadata CacheDelegate(GetAssemblyMetadata getAssemblyMetadataValue)
        {
            getAssemblyMetadata = getAssemblyMetadataValue;
            return GetAssemblyMetadata;
        }

        /// <summary>
        /// Cache the results of a FileExists delegate.
        /// </summary>
        /// <returns>Cached version of the delegate.</returns>
        internal FileExists CacheDelegate()
        {
            return FileExists;
        }

        public DirectoryExists CacheDelegate(DirectoryExists directoryExistsValue)
        {
            directoryExists = directoryExistsValue;
            return DirectoryExists;
        }

        /// <summary>
        /// Cache the results of a GetDirectories delegate.
        /// </summary>
        /// <param name="getDirectoriesValue">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal GetDirectories CacheDelegate(GetDirectories getDirectoriesValue)
        {
            getDirectories = getDirectoriesValue;
            return GetDirectories;
        }

        /// <summary>
        /// Cache the results of a GetAssemblyRuntimeVersion delegate.
        /// </summary>
        /// <param name="getAssemblyRuntimeVersion">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal GetAssemblyRuntimeVersion CacheDelegate(GetAssemblyRuntimeVersion getAssemblyRuntimeVersion)
        {
            this.getAssemblyRuntimeVersion = getAssemblyRuntimeVersion;
            return GetRuntimeVersion;
        }

        internal FileState GetFileState(string path)
        {
            // Looking up an assembly to get its metadata can be expensive for projects that reference large amounts
            // of assemblies. To avoid that expense, we remember and serialize this information betweeen runs in
            // <ProjectFileName>.AssemblyReference.cache files in the intermediate directory and also store it in an
            // process-wide cache to share between successive builds.
            //
            // To determine if this information is up-to-date, we use the last modified date of the assembly, however,
            // as calls to GetLastWriteTime can add up over hundreds and hundreds of files, we only check for
            // invalidation once per assembly per ResolveAssemblyReference session.

            upToDateLocalFileStateCache.TryGetValue(path, out FileState state);
            if (state == null)
            {   // We haven't seen this file this ResolveAssemblyReference session
                state = ComputeFileStateFromCachesAndDisk(path);
                upToDateLocalFileStateCache[path] = state;
            }

            return state;
        }

        private FileState ComputeFileStateFromCachesAndDisk(string path)
        {
            DateTime lastModified = GetAndCacheLastModified(path);
            bool isCachedInInstance = instanceLocalFileStateCache.TryGetValue(path, out FileState cachedInstanceFileState);
            bool isCachedInProcess = s_processWideFileStateCache.TryGetValue(path, out FileState cachedProcessFileState);

            bool isInstanceFileStateUpToDate = isCachedInInstance && lastModified == cachedInstanceFileState.LastModified;
            bool isProcessFileStateUpToDate = isCachedInProcess && lastModified == cachedProcessFileState.LastModified;

            // If the process-wide cache contains an up-to-date FileState, always use it.
            if (isProcessFileStateUpToDate)
            {
                // For the next build, we may be using a different process. Update the file cache if the entry is worth persisting.
                if (cachedProcessFileState.IsWorthPersisting)
                {
                    if (!isInstanceFileStateUpToDate)
                    {
                        instanceLocalFileStateCache[path] = cachedProcessFileState;
                        isDirty = true;
                    }

                    // Remember that this FileState was actually used by adding it to the outgoing dictionary.
                    instanceLocalOutgoingFileStateCache[path] = cachedProcessFileState;
                }
                return cachedProcessFileState;
            }
            if (isInstanceFileStateUpToDate)
            {
                if (cachedInstanceFileState.IsWorthPersisting)
                {
                    // Remember that this FileState was actually used by adding it to the outgoing dictionary.
                    instanceLocalOutgoingFileStateCache[path] = cachedInstanceFileState;
                }
                return s_processWideFileStateCache[path] = cachedInstanceFileState;
            }

            // If no up-to-date FileState exists at this point, create one and take ownership
            return InitializeFileState(path, lastModified);
        }

        private DateTime GetAndCacheLastModified(string path)
        {
            if (!instanceLocalLastModifiedCache.TryGetValue(path, out DateTime lastModified))
            {
                lastModified = getLastWriteTime(path);
                instanceLocalLastModifiedCache[path] = lastModified;
            }

            return lastModified;
        }

        private FileState InitializeFileState(string path, DateTime lastModified)
        {
            var fileState = new FileState(lastModified);

            // Dirty the instance-local cache only with entries that are worth persisting.
            if (fileState.IsWorthPersisting)
            {
                instanceLocalFileStateCache[path] = fileState;
                instanceLocalOutgoingFileStateCache[path] = fileState;
                isDirty = true;
            }

            s_processWideFileStateCache[path] = fileState;

            return fileState;
        }

        /// <summary>
        /// Cached implementation of GetAssemblyName.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>The assembly name.</returns>
        private AssemblyNameExtension GetAssemblyName(string path)
        {
            // If the assembly is in an FX folder and its a well-known assembly
            // then we can short-circuit the File IO involved with GetAssemblyName()
            if (redistList != null)
            {
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    IEnumerable<AssemblyEntry> assemblyNames = redistList.FindAssemblyNameFromSimpleName(
                            Path.GetFileNameWithoutExtension(path));
                    string filename = Path.GetFileName(path);

                    foreach (AssemblyEntry a in assemblyNames)
                    {
                        string pathFromRedistList = Path.Combine(a.FrameworkDirectory, filename);

                        if (String.Equals(path, pathFromRedistList, StringComparison.OrdinalIgnoreCase))
                        {
                            return new AssemblyNameExtension(a.FullName);
                        }
                    }
                }
            }

            // Not a well-known FX assembly so now check the cache.
            FileState fileState = GetFileState(path);
            if (fileState.Assembly == null)
            {
                fileState.Assembly = getAssemblyName(path);

                // Certain assemblies, like mscorlib may not have metadata.
                // Avoid continuously calling getAssemblyName on these files by
                // recording these as having an empty name.
                if (fileState.Assembly == null)
                {
                    fileState.Assembly = AssemblyNameExtension.UnnamedAssembly;
                }
                if (fileState.IsWorthPersisting)
                {
                    isDirty = true;
                }
            }

            if (fileState.Assembly.IsUnnamedAssembly)
            {
                return null;
            }

            return fileState.Assembly;
        }

        /// <summary>
        /// Cached implementation. Given a path, crack it open and retrieve runtimeversion for the assembly.
        /// </summary>
        /// <param name="path">Path to the assembly.</param>
        private string GetRuntimeVersion(string path)
        {
            FileState fileState = GetFileState(path);
            if (String.IsNullOrEmpty(fileState.RuntimeVersion))
            {
                fileState.RuntimeVersion = getAssemblyRuntimeVersion(path);
                if (fileState.IsWorthPersisting)
                {
                    isDirty = true;
                }
            }

            return fileState.RuntimeVersion;
        }

        /// <summary>
        /// Cached implementation. Given an assembly name, crack it open and retrieve the list of dependent
        /// assemblies and  the list of scatter files.
        /// </summary>
        /// <param name="path">Path to the assembly.</param>
        /// <param name="assemblyMetadataCache">Cache for pre-extracted assembly metadata.</param>
        /// <param name="dependencies">Receives the list of dependencies.</param>
        /// <param name="scatterFiles">Receives the list of associated scatter files.</param>
        /// <param name="frameworkName"></param>
        private void GetAssemblyMetadata(
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> assemblyMetadataCache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName frameworkName)
        {
            FileState fileState = GetFileState(path);
            if (fileState.dependencies == null)
            {
                getAssemblyMetadata(
                    path,
                    assemblyMetadataCache,
                    out fileState.dependencies,
                    out fileState.scatterFiles,
                    out fileState.frameworkName);

                if (fileState.IsWorthPersisting)
                {
                    isDirty = true;
                }
            }

            dependencies = fileState.dependencies;
            scatterFiles = fileState.scatterFiles;
            frameworkName = fileState.frameworkName;
        }

        /// <summary>
        /// Reads in cached data from stateFiles to build an initial cache. Avoids logging warnings or errors.
        /// </summary>
        /// <param name="stateFiles">List of locations of caches on disk.</param>
        /// <param name="log">How to log</param>
        /// <param name="fileExists">Whether a file exists</param>
        /// <returns>A cache representing key aspects of file states.</returns>
        internal static SystemState DeserializePrecomputedCaches(ITaskItem[] stateFiles, TaskLoggingHelper log, FileExists fileExists)
        {
            SystemState retVal = new SystemState();
            retVal.isDirty = stateFiles.Length > 0;
            HashSet<string> assembliesFound = new HashSet<string>();

            foreach (ITaskItem stateFile in stateFiles)
            {
                // Verify that it's a real stateFile. Log message but do not error if not.
                SystemState sysState = DeserializeCache<SystemState>(stateFile.ToString(), log);
                if (sysState == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, FileState> kvp in sysState.instanceLocalFileStateCache)
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
            // Save a copy of instanceLocalOutgoingFileStateCache so we can restore it later. SerializeCacheByTranslator serializes
            // instanceLocalOutgoingFileStateCache by default, so change that to the relativized form, then change it back.
            Dictionary<string, FileState> oldFileStateCache = instanceLocalOutgoingFileStateCache;
            instanceLocalOutgoingFileStateCache = instanceLocalFileStateCache.ToDictionary(kvp => FileUtilities.MakeRelative(Path.GetDirectoryName(stateFile), kvp.Key), kvp => kvp.Value);

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
                instanceLocalOutgoingFileStateCache = oldFileStateCache;
            }
        }

        /// <summary>
        /// Cached implementation of GetDirectories.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns>The list of directories from the specified path.</returns>
        private string[] GetDirectories(string path, string pattern)
        {
            // Only cache the *. pattern. This is by far the most common pattern
            // and generalized caching would require a call to Path.Combine which
            // is a string-copy.
            if (pattern == "*")
            {
                instanceLocalDirectories.TryGetValue(path, out string[] cached);
                if (cached == null)
                {
                    string[] directories = getDirectories(path, pattern);
                    instanceLocalDirectories[path] = directories;
                    return directories;
                }
                return cached;
            }

            // This path is currently uncalled. Use assert to tell the dev that adds a new code-path
            // that this is an unoptimized path.
            Debug.Assert(false, "Using slow-path in SystemState.GetDirectories, was this intentional?");

            return getDirectories(path, pattern);
        }

        /// <summary>
        /// Cached implementation of FileExists.
        /// </summary>
        /// <param name="path">Path to file.</param>
        /// <returns>True if the file exists.</returns>
        private bool FileExists(string path)
        {
            DateTime lastModified = GetAndCacheLastModified(path);
            return FileTimestampIndicatesFileExists(lastModified);
        }

        private bool FileTimestampIndicatesFileExists(DateTime lastModified)
        {
            // TODO: Standardize LastWriteTime value for nonexistent files. See https://github.com/dotnet/msbuild/issues/3699
            return lastModified != DateTime.MinValue && lastModified != NativeMethodsShared.MinFileDate;
        }

        /// <summary>
        /// Cached implementation of DirectoryExists.
        /// </summary>
        /// <param name="path">Path to file.</param>
        /// <returns>True if the directory exists.</returns>
        private bool DirectoryExists(string path)
        {
            if (instanceLocalDirectoryExists.TryGetValue(path, out bool flag))
            {
                return flag;
            }

            bool exists = directoryExists(path);
            instanceLocalDirectoryExists[path] = exists;
            return exists;
        }
    }
}
