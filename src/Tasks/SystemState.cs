// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.AssemblyDependency;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Class is used to cache system state.
    /// </summary>
    internal sealed class SystemState : ResolveAssemblyReferenceCache
    {
        /// <summary>
        /// Cache at the SystemState instance level. Has the same contents as <see cref="ResolveAssemblyReferenceCache.instanceLocalFileStateCache"/>.
        /// It acts as a flag to enforce that an entry has been checked for staleness only once.
        /// </summary>
        private Dictionary<string, FileState> upToDateLocalFileStateCache = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

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
        /// A callback to invoke to lazily load the serialized <see cref="ResolveAssemblyReferenceCache"/> from disk.
        /// Null if lazy loading is disabled or if the cache has already been loaded.
        /// </summary>
        private Func<ResolveAssemblyReferenceCache> _loadDiskCacheCallback;

        /// <summary>
        /// True if we've already loaded (or attempted to load) the disk cache.
        /// </summary>
        internal bool IsDiskCacheLoaded => _loadDiskCacheCallback == null;

        /// <summary>
        /// XML tables of installed assemblies.
        /// </summary>
        private RedistList redistList;

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
        /// Construct.
        /// </summary>
        public SystemState()
        {
        }

        /// <summary>
        /// Construct.
        /// </summary>
        public SystemState(Func<ResolveAssemblyReferenceCache> loadDiskCacheCallback, bool loadLazily)
            : base(loadLazily ? null : loadDiskCacheCallback())
        {
            _loadDiskCacheCallback = loadLazily ? loadDiskCacheCallback : null;
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

        /// <summary>
        /// Sets <see cref="ResolveAssemblyReferenceCache.IsDirty"/> flag to true if we have loaded (or attempted to load) the disk casche.
        /// </summary>
        private void SetIsDirty()
        {
            if (IsDiskCacheLoaded)
            {
                isDirty = true;
            }
        }

        private FileState GetFileState(string path)
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

            if (!isProcessFileStateUpToDate && !isInstanceFileStateUpToDate && lastModified != FileState.ImmutableFileLastModifiedMarker)
            {
                // If we haven't loaded the disk cache yet, do it now.
                if (EnsureResolveAssemblyReferenceCacheLoaded())
                {
                    isCachedInInstance = instanceLocalFileStateCache.TryGetValue(path, out cachedInstanceFileState);
                    isInstanceFileStateUpToDate = isCachedInInstance && lastModified == cachedInstanceFileState.LastModified;
                }
            }

            // If the process-wide cache contains an up-to-date FileState, always use it
            if (isProcessFileStateUpToDate)
            {
                // For the next build, we may be using a different process. Update the file cache if the entry is worth persisting.
                if (!isInstanceFileStateUpToDate && cachedProcessFileState.IsWorthPersisting)
                {
                    instanceLocalFileStateCache[path] = cachedProcessFileState;
                    SetIsDirty();
                }
                return cachedProcessFileState;
            }
            if (isInstanceFileStateUpToDate)
            {
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
                SetIsDirty();
            }

            s_processWideFileStateCache[path] = fileState;

            return fileState;
        }

        /// <summary>
        /// Loads the on-disk cache if it has not been attempted before during the current RAR execution.
        /// </summary>
        /// <returns>
        /// True if this method loaded the cache, false otherwise.
        /// </returns>
        internal bool EnsureResolveAssemblyReferenceCacheLoaded()
        {
            if (IsDiskCacheLoaded)
            {
                // We've already loaded (or attempted to load) the disk cache, nothing to do.
                return false;
            }

            ResolveAssemblyReferenceCache diskCache = _loadDiskCacheCallback();
            _loadDiskCacheCallback = null;

            if (diskCache != null)
            {
                // If we successully loaded the cache from disk, merge it with what we already have.
                MergeInstanceLocalFileStateCache(diskCache, this);
                return true;
            }
            return false;
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

                if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
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
                    SetIsDirty();
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
                    SetIsDirty();
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
            if (fileState.Dependencies == null)
            {
                getAssemblyMetadata(
                    path,
                    assemblyMetadataCache,
                    out dependencies,
                    out scatterFiles,
                    out frameworkName);

                fileState.SetAssemblyMetadata(dependencies, scatterFiles, frameworkName);

                if (fileState.IsWorthPersisting)
                {
                    SetIsDirty();
                }
            }
            else
            {
                dependencies = fileState.Dependencies;
                scatterFiles = fileState.ScatterFiles;
                frameworkName = fileState.FrameworkName;
            }
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
