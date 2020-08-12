// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.AssemblyDependency;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// File.GetAttributes delegate
    /// </summary>
    /// <param name="path">The path get attributes for.</param>
    internal delegate FileAttributes GetAttributes(string path);

    /// <summary>
    /// File SetAttributes delegate
    /// </summary>
    /// <param name="path">The path to set attributes for.</param>
    /// <param name="attributes">The actual file attributes.</param>
    internal delegate void SetAttributes(string path, FileAttributes attributes);

    /// <summary>
    /// File SetLastAccessTime delegate.
    /// </summary>
    internal delegate void SetLastAccessTime(string path, DateTime timestamp);

    /// <summary>
    /// File SetLastWriteTime delegate.
    /// </summary>
    internal delegate void SetLastWriteTime(string path, DateTime timestamp);

    /// <summary>
    /// GetDirectories delegate
    /// </summary>
    /// <param name="path">The path to get directories for.</param>
    /// <param name="pattern">The pattern to search for.</param>
    /// <returns>An array of directories.</returns>
    internal delegate string[] GetDirectories(string path, string pattern);

    /// <summary>
    /// CopyFile delegate
    /// </summary>
    /// <param name="source">Source file</param>
    /// <param name="destination">Destination file</param>
    internal delegate bool CopyFile(string source, string destination);

    /// <summary>
    /// GetAssemblyName delegate
    /// </summary>
    /// <param name="path">The path to the file</param>
    /// <returns>The assembly name.</returns>
    internal delegate AssemblyNameExtension GetAssemblyName(string path);

    /// <summary>
    /// GetAssemblyRuntimeVersion delegate to get the clr runtime version of a file.
    /// </summary>
    /// <param name="path">The path to the file</param>
    /// <returns>The clr runtime version for the file</returns>
    internal delegate string GetAssemblyRuntimeVersion(string path);

    /// <summary>
    /// GetGacEnumerator delegate to get the enumerator which will enumerate over the GAC
    /// </summary>
    /// <param name="strongName">StrongName to get an enumerator for</param>
    /// <returns>The enumerator for the gac</returns>
    internal delegate IEnumerable<AssemblyNameExtension> GetGacEnumerator(string strongName);

    /// <summary>
    /// GetPathFromFusionName delegate to get path to a file based on the fusion name
    /// </summary>
    /// <param name="strongName">StrongName to get a path for</param>
    /// <returns>The path to the assembly</returns>
    internal delegate string GetPathFromFusionName(string strongName);

    /// <summary>
    /// Delegate. Given an assembly name, crack it open and retrieve the list of dependent 
    /// assemblies and  the list of scatter files.
    /// </summary>
    /// <param name="path">Path to the assembly.</param>
    /// <param name="assemblyMetadataCache">Assembly metadata cache.</param>
    /// <param name="dependencies">Receives the list of dependencies.</param>
    /// <param name="scatterFiles">Receives the list of associated scatter files.</param>
    /// <param name="frameworkNameAttribute">The framework name</param>
    internal delegate void GetAssemblyMetadata
    (
        string path,
        ConcurrentDictionary<string, AssemblyMetadata> assemblyMetadataCache,
        out AssemblyNameExtension[] dependencies,
        out string[] scatterFiles,
        out FrameworkName frameworkNameAttribute
    );

    /// <summary>
    /// Delegate to take in a dll path and read the machine type from the PEHeader
    /// </summary>
    internal delegate UInt16 ReadMachineTypeFromPEHeader(string dllPath);

    /// <summary>
    /// Delegate to get the path to an assembly in the GAC.
    /// </summary>
    internal delegate string GetAssemblyPathInGac(AssemblyNameExtension assemblyName, System.Reflection.ProcessorArchitecture targetProcessorArchitecture, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVersion, FileExists fileExists, bool fullFusionName, bool specificVersion);

    /// <summary>
    /// Determines if a assembly is an winmd file 
    /// </summary>
    internal delegate bool IsWinMDFile(string fullpath, GetAssemblyRuntimeVersion getAssemblyRuntimeVersion, FileExists fileExists, out string imageRuntimeVersion, out bool isManagedWinmd);

    /// <summary>
    /// CreateFileString delegate. Creates a stream on top of a file.
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="mode">File mode</param>
    /// <param name="access">Access type</param>
    /// <returns>The Stream</returns>
    internal delegate Stream CreateFileStream(string path, FileMode mode, FileAccess access);

    /// <summary>
    /// Delegate for System.IO.File.GetLastWriteTime
    /// </summary>
    /// <param name="path">The file name</param>
    /// <returns>The last write time.</returns>
    internal delegate DateTime GetLastWriteTime(string path);
}
