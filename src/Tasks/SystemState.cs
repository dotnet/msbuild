// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Security.Permissions;

using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;
using AssemblyNameExtension = Microsoft.Build.Shared.AssemblyNameExtension;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Class is used to cache system state.
    /// </summary>
    internal sealed partial class SystemState
    {
        /// <summary>
        /// Cache at the SystemState instance level. Has the same contents as <see cref="instanceLocalFileStateCache"/>.
        /// It acts as a flag to enforce that an entry has been checked for staleness only once.
        /// </summary>
        private Dictionary<string, FileState> upToDateLocalFileStateCache = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Cache at the SystemState instance level. It is serialized and reused between instances.
        /// </summary>
        private Dictionary<string, FileState> instanceLocalFileStateCache = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

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
        private static ConcurrentDictionary<string, FileState> s_processWideFileStateCache = new ConcurrentDictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

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
        private FileExists fileExists;

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
        private sealed class FileState
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
        }

        internal static void InitializeSerializer()
        {
            StateFileCache<SystemStateCachePayload>.InitializeSerializer();
        }

        /// <summary>
        /// Reads the contents from the specified file into the instance local cache.
        /// </summary>
        /// <returns>The deserialized instance local cache.</returns>
        internal static SystemState DeserializeCache(string stateFile, TaskLoggingHelper log)
        {
            SystemStateCachePayload cachePayload = StateFileCache<SystemStateCachePayload>.DeserializeCache(stateFile, log);

            if (cachePayload == null)
            {
                return null;
            }

            Dictionary<string, FileState> cache = SystemStateCacheHydrator.HydrateSystemStateCache(cachePayload);

            return new SystemState { instanceLocalFileStateCache = cache };
        }

        /// <summary>
        /// Writes the contents of the instance local cache out to the specified file.
        /// </summary>
        internal void SerializeCache(string stateFile, TaskLoggingHelper log)
        {
            SystemStateCachePayload cachePayload = SystemStateCachePayloadExtractor.ExtractSystemStateCachePayload(instanceLocalFileStateCache);
            StateFileCache<SystemStateCachePayload>.SerializeCache(stateFile, log, cachePayload);
        }

        /// <summary>
        /// Set the target framework paths.
        /// This is used to optimize IO in the case of files requested from one 
        /// of the FX folders.
        /// </summary>
        /// <param name="providedFrameworkPaths"></param>
        /// <param name="installedAssemblyTables"></param>
        internal void SetInstalledAssemblyInformation
        (
            AssemblyTableInfo[] installedAssemblyTableInfos
        )
        {
            redistList = RedistList.GetRedistList(installedAssemblyTableInfos);
        }

        /// <summary>
        /// Flag that indicates
        /// </summary>
        /// <value></value>
        internal bool IsDirty
        {
            get { return isDirty; }
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
        /// <param name="fileExistsValue">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal FileExists CacheDelegate(FileExists fileExistsValue)
        {
            fileExists = fileExistsValue;
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

        private FileState GetFileState(string path)
        {
            // Looking up an assembly to get its metadata can be expensive for projects that reference large amounts
            // of assemblies. To avoid that expense, we remember and serialize this information betweeen runs in
            // XXXResolveAssemblyReferencesInput.cache files in the intermediate directory and also store it in an
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
            // Is it in the process-wide cache?
            FileState cacheFileState = null;
            FileState processFileState = null;
            s_processWideFileStateCache.TryGetValue(path, out processFileState);
            instanceLocalFileStateCache.TryGetValue(path, out FileState instanceLocalFileState);

            // Sync the caches.
            if (processFileState == null && instanceLocalFileState != null)
            {
                cacheFileState = instanceLocalFileState;
                SystemState.s_processWideFileStateCache[path] = instanceLocalFileState;
            }
            else if (processFileState != null && instanceLocalFileState == null)
            {
                cacheFileState = processFileState;
                instanceLocalFileStateCache[path] = processFileState;
            }
            else if (processFileState != null && instanceLocalFileState != null)
            {
                if (processFileState.LastModified > instanceLocalFileState.LastModified)
                {
                    cacheFileState = processFileState;
                    instanceLocalFileStateCache[path] = processFileState;
                }
                else
                {
                    cacheFileState = instanceLocalFileState;
                    SystemState.s_processWideFileStateCache[path] = instanceLocalFileState;
                }
            }

            bool isLastModifiedCached = instanceLocalLastModifiedCache.TryGetValue(path, out DateTime lastModified);
            if (!isLastModifiedCached)
            {
                lastModified = getLastWriteTime(path);
                instanceLocalLastModifiedCache[path] = lastModified;
            }

            // Still no--need to create.            
            if (cacheFileState == null) // Or check time stamp
            {
                cacheFileState = new FileState(lastModified);
                instanceLocalFileStateCache[path] = cacheFileState;
                SystemState.s_processWideFileStateCache[path] = cacheFileState;
                isDirty = true;
            }
            else
            {
                // If time stamps have changed, then purge.
                if (lastModified != cacheFileState.LastModified)
                {
                    cacheFileState = new FileState(lastModified);
                    instanceLocalFileStateCache[path] = cacheFileState;
                    SystemState.s_processWideFileStateCache[path] = cacheFileState;
                    isDirty = true;
                }
            }

            return cacheFileState;
        }

        private FileState GetFileStateFromProcessWideCache(string path, FileState template)
        {
            // When reading from the process-wide cache, we always check to see if our data
            // is up-to-date to avoid getting stale data from a previous build.
            DateTime lastModified = getLastWriteTime(path);

            // Has another build seen this file before?
            FileState state;
            if (!s_processWideFileStateCache.TryGetValue(path, out state) || state.LastModified != lastModified)
            {   // We've never seen it before, or we're out of date

                state = CreateFileState(lastModified, template);
                s_processWideFileStateCache[path] = state;
            }

            return state;
        }

        private FileState CreateFileState(DateTime lastModified, FileState template)
        {
            if (template != null && template.LastModified == lastModified)
                return template;    // Our serialized data is up-to-date

            return new FileState(lastModified);
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
                string extension = Path.GetExtension(path);
                if (String.Compare(extension, ".dll", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    IEnumerable<AssemblyEntry> assemblyNames = redistList.FindAssemblyNameFromSimpleName
                        (
                            Path.GetFileNameWithoutExtension(path)
                        );

                    foreach (AssemblyEntry a in assemblyNames)
                    {
                        string filename = Path.GetFileName(path);
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
                isDirty = true;
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
                isDirty = true;
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
        private void GetAssemblyMetadata
        (
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> assemblyMetadataCache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName frameworkName
        )
        {
            FileState fileState = GetFileState(path);
            if (fileState.dependencies == null)
            {
                getAssemblyMetadata
                (
                    path,
                    assemblyMetadataCache,
                    out fileState.dependencies,
                    out fileState.scatterFiles,
                    out fileState.frameworkName
                 );

                isDirty = true;
            }

            dependencies = fileState.dependencies;
            scatterFiles = fileState.scatterFiles;
            frameworkName = fileState.frameworkName;
        }

        /// <summary>
        /// Cached implementation of GetDirectories.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
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
            if (instanceLocalLastModifiedCache.TryGetValue(path, out DateTime cachedLastModified))
            {
                return FileTimestampIndicatesFileExists(cachedLastModified);
            }

            // Cache miss; fall back to actual check and populate cache with result
            DateTime lastModified = getLastWriteTime(path);
            instanceLocalLastModifiedCache[path] = lastModified;
            return FileTimestampIndicatesFileExists(lastModified);
        }

        private bool FileTimestampIndicatesFileExists(DateTime lastModified)
        {
            // TODO: Standardize LastWriteTime value for nonexistent files. See https://github.com/Microsoft/msbuild/issues/3699
            return lastModified != DateTime.MinValue && lastModified != DateTime.FromFileTimeUtc(0);
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
