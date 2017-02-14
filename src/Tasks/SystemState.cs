// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Reflection;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Class is used to cache system state.
    /// </summary>
    [Serializable]
    internal sealed class SystemState : StateFileBase, ISerializable
    {
        /// <summary>
        /// State information for cached files kept at the SystemState instance level.
        /// </summary>
        private Hashtable instanceLocalFileStateCache = new Hashtable();

        /// <summary>
        /// FileExists information is purely instance-local. It doesn't make sense to
        /// cache this for long periods of time since there's no way (without actually 
        /// calling File.Exists) to tell whether the cache is out-of-date.
        /// </summary>
        private Hashtable instanceLocalFileExists = new Hashtable();

        /// <summary>
        /// GetDirectories information is also purely instance-local. This information
        /// is only considered good for the lifetime of the task (or whatever) that owns 
        /// this instance.
        /// </summary>
        private Hashtable instanceLocalDirectories = new Hashtable();

        /// <summary>
        /// Additional level of caching kept at the process level.
        /// </summary>
        private static ConcurrentDictionary<string, FileState> s_processWideFileStateCache = new ConcurrentDictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// XML tables of installed assemblies.
        /// </summary>
        private RedistList redistList = null;

        /// <summary>
        /// True if the contents have changed.
        /// </summary>
        private bool isDirty = false;

        /// <summary>
        /// Delegate used internally.
        /// </summary>
        private GetLastWriteTime getLastWriteTime = null;

        /// <summary>
        /// Cached delegate.
        /// </summary>
        private GetAssemblyName getAssemblyName = null;

        /// <summary>
        /// Cached delegate.
        /// </summary>
        private GetAssemblyMetadata getAssemblyMetadata = null;

        /// <summary>
        /// Cached delegate.
        /// </summary>
        private FileExists fileExists = null;

        /// <summary>
        /// Cached delegate.
        /// </summary>
        private GetDirectories getDirectories = null;

        /// <summary>
        /// Cached delegate
        /// </summary>
        private GetAssemblyRuntimeVersion getAssemblyRuntimeVersion = null;

        /// <summary>
        /// Class that holds the current file state.
        /// </summary>
        [Serializable]
        private sealed class FileState : ISerializable
        {
            /// <summary>
            /// The last modified time for this file.
            /// </summary>
            private DateTime lastModified;

            /// <summary>
            /// The fusion name of this file.
            /// </summary>
            private AssemblyNameExtension assemblyName = null;

            /// <summary>
            /// The assemblies that this file depends on.
            /// </summary>
            internal AssemblyNameExtension[] dependencies = null;

            /// <summary>
            /// The scatter files associated with this assembly.
            /// </summary>
            internal string[] scatterFiles = null;

            /// <summary>
            /// FrameworkName the file was built against
            /// </summary>
            internal FrameworkName frameworkName = null;

            /// <summary>
            /// The CLR runtime version for the assembly.
            /// </summary>
            internal string runtimeVersion = null;

            /// <summary>
            /// Default construct.
            /// </summary>
            internal FileState()
            {
            }

            /// <summary>
            /// Deserializing constuctor.
            /// </summary>
            internal FileState(SerializationInfo info, StreamingContext context)
            {
                ErrorUtilities.VerifyThrowArgumentNull(info, "info");

                lastModified = info.GetDateTime("lastModified");
                assemblyName = (AssemblyNameExtension)info.GetValue("assemblyName", typeof(AssemblyNameExtension));
                dependencies = (AssemblyNameExtension[])info.GetValue("dependencies", typeof(AssemblyNameExtension[]));
                scatterFiles = (string[])info.GetValue("scatterFiles", typeof(string[]));
                runtimeVersion = (string)info.GetValue("runtimeVersion", typeof(string));
                frameworkName = (FrameworkName)info.GetValue("frameworkName", typeof(FrameworkName));
            }

            /// <summary>
            /// Serialize the contents of the class.
            /// </summary>
            [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                ErrorUtilities.VerifyThrowArgumentNull(info, "info");

                info.AddValue("lastModified", lastModified);
                info.AddValue("assemblyName", assemblyName);
                info.AddValue("dependencies", dependencies);
                info.AddValue("scatterFiles", scatterFiles);
                info.AddValue("runtimeVersion", runtimeVersion);
                info.AddValue("frameworkName", frameworkName);
            }

            /// <summary>
            /// Get or set the assemblyName.
            /// </summary>
            /// <value></value>
            internal DateTime LastModified
            {
                get { return lastModified; }
                set { lastModified = value; }
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
                get { return this.runtimeVersion; }
                set { this.runtimeVersion = value; }
            }

            /// <summary>
            /// Get or set the framework name the file was built against
            /// </summary>
            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Could be used in other assemblies")]
            internal FrameworkName FrameworkNameAttribute
            {
                get { return this.frameworkName; }
                set { this.frameworkName = value; }
            }
        }

        /// <summary>
        /// Construct.
        /// </summary>
        internal SystemState()
        {
        }

        /// <summary>
        /// Deserialize the contents of the class.
        /// </summary>
        internal SystemState(SerializationInfo info, StreamingContext context)
        {
            ErrorUtilities.VerifyThrowArgumentNull(info, "info");

            instanceLocalFileStateCache = (Hashtable)info.GetValue("fileState", typeof(Hashtable));
            isDirty = false;
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
        /// Serialize the contents of the class.
        /// </summary>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ErrorUtilities.VerifyThrowArgumentNull(info, "info");

            info.AddValue("fileState", instanceLocalFileStateCache);
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
        /// <param name="getLastWriteTime">Delegate used to get the last write time.</param>
        internal void SetGetLastWriteTime(GetLastWriteTime getLastWriteTimeValue)
        {
            getLastWriteTime = getLastWriteTimeValue;
        }

        /// <summary>
        /// Cache the results of a GetAssemblyName delegate. 
        /// </summary>
        /// <param name="getAssemblyName">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal GetAssemblyName CacheDelegate(GetAssemblyName getAssemblyNameValue)
        {
            getAssemblyName = getAssemblyNameValue;
            return new GetAssemblyName(this.GetAssemblyName);
        }

        /// <summary>
        /// Cache the results of a GetAssemblyMetadata delegate. 
        /// </summary>
        /// <param name="getAssemblyMetadata">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal GetAssemblyMetadata CacheDelegate(GetAssemblyMetadata getAssemblyMetadataValue)
        {
            getAssemblyMetadata = getAssemblyMetadataValue;
            return new GetAssemblyMetadata(this.GetAssemblyMetadata);
        }

        /// <summary>
        /// Cache the results of a FileExists delegate. 
        /// </summary>
        /// <param name="fileExists">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal FileExists CacheDelegate(FileExists fileExistsValue)
        {
            fileExists = fileExistsValue;
            return new FileExists(this.FileExists);
        }

        /// <summary>
        /// Cache the results of a GetDirectories delegate. 
        /// </summary>
        /// <param name="getDirectories">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal GetDirectories CacheDelegate(GetDirectories getDirectoriesValue)
        {
            getDirectories = getDirectoriesValue;
            return new GetDirectories(this.GetDirectories);
        }

        /// <summary>
        /// Cache the results of a GetAssemblyRuntimeVersion delegate. 
        /// </summary>
        /// <param name="getAssemblyRuntimeVersion">The delegate.</param>
        /// <returns>Cached version of the delegate.</returns>
        internal GetAssemblyRuntimeVersion CacheDelegate(GetAssemblyRuntimeVersion getAssemblyRuntimeVersion)
        {
            this.getAssemblyRuntimeVersion = getAssemblyRuntimeVersion;
            return new GetAssemblyRuntimeVersion(this.GetRuntimeVersion);
        }

        /// <summary>
        /// Retrieve the file state object for this path. Create if necessary.
        /// </summary>
        /// <param name="path">The name of the file.</param>
        /// <returns>The file state object.</returns>
        private FileState GetFileState(string path)
        {
            // Is it in the process-wide cache?
            FileState cacheFileState = null;
            FileState processFileState = null;
            SystemState.s_processWideFileStateCache.TryGetValue(path, out processFileState);
            FileState instanceLocalFileState = instanceLocalFileState = (FileState)instanceLocalFileStateCache[path];

            // Sync the caches.
            if (processFileState == null && instanceLocalFileState != null)
            {
                cacheFileState = instanceLocalFileState;
                SystemState.s_processWideFileStateCache.TryAdd(path, instanceLocalFileState);
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
                    SystemState.s_processWideFileStateCache.TryAdd(path, instanceLocalFileState);
                }
            }

            // Still no--need to create.            
            if (cacheFileState == null) // Or check time stamp
            {
                cacheFileState = new FileState();
                cacheFileState.LastModified = getLastWriteTime(path);
                instanceLocalFileStateCache[path] = cacheFileState;
                SystemState.s_processWideFileStateCache.TryAdd(path, cacheFileState);
                isDirty = true;
            }
            else
            {
                // If time stamps have changed, then purge.
                DateTime lastModified = getLastWriteTime(path);
                if (lastModified != cacheFileState.LastModified)
                {
                    cacheFileState = new FileState();
                    cacheFileState.LastModified = getLastWriteTime(path);
                    instanceLocalFileStateCache[path] = cacheFileState;
                    SystemState.s_processWideFileStateCache.TryAdd(path, cacheFileState);
                    isDirty = true;
                }
            }

            return cacheFileState;
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
                    AssemblyEntry[] assemblyNames = redistList.FindAssemblyNameFromSimpleName
                        (
                            Path.GetFileNameWithoutExtension(path)
                        );

                    for (int i = 0; i < assemblyNames.Length; ++i)
                    {
                        string filename = Path.GetFileName(path);
                        string pathFromRedistList = Path.Combine(assemblyNames[i].FrameworkDirectory, filename);

                        if (String.Equals(path, pathFromRedistList, StringComparison.OrdinalIgnoreCase))
                        {
                            return new AssemblyNameExtension(assemblyNames[i].FullName);
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
        /// <param name="dependencies">Receives the list of dependencies.</param>
        /// <param name="scatterFiles">Receives the list of associated scatter files.</param>
        private void GetAssemblyMetadata
        (
            string path,
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
                object cached = instanceLocalDirectories[path];
                if (cached == null)
                {
                    string[] directories = getDirectories(path, pattern);
                    instanceLocalDirectories[path] = directories;
                    return directories;
                }
                return (string[])cached;
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
            /////////////////////////////////////////////////////////////////////////////////////////////
            // FIRST -- Look in the primary cache for this path.
            /////////////////////////////////////////////////////////////////////////////////////////////
            object flag = instanceLocalFileExists[path];
            if (flag != null)
            {
                return (bool)flag;
            }

            /////////////////////////////////////////////////////////////////////////////////////////////
            // SECOND -- fall back to plain old File.Exists and cache the result.
            /////////////////////////////////////////////////////////////////////////////////////////////
            bool exists = fileExists(path);
            instanceLocalFileExists[path] = exists;
            return exists;
        }
    }
}
