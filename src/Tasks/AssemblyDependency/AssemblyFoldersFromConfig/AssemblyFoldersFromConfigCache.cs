// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks.AssemblyFoldersFromConfig
{
    /// <summary>
    /// Contains information about entries in the AssemblyFoldersEx registry keys.
    /// </summary>
    internal class AssemblyFoldersFromConfigCache
    {
        /// <summary>
        /// Set of files in ALL AssemblyFolderFromConfig directories
        /// </summary>
        private readonly ImmutableHashSet<string> _filesInDirectories;

        /// <summary>
        /// File exists delegate we are replacing
        /// </summary>
        private readonly FileExists _fileExists;

        /// <summary>
        /// Should we use the original on or use our own
        /// </summary>
        private readonly bool _useOriginalFileExists;

        /// <summary>
        /// Constructor
        /// </summary>
        internal AssemblyFoldersFromConfigCache(AssemblyFoldersFromConfig assemblyFoldersFromConfig, FileExists fileExists, TaskEnvironment taskEnvironment)
        {
            AssemblyFoldersFromConfig = assemblyFoldersFromConfig;
            _fileExists = fileExists;

            if (Environment.GetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE") != null)
            {
                _useOriginalFileExists = true;
            }
            else
            {
                _filesInDirectories = assemblyFoldersFromConfig.Select(assemblyFolder => taskEnvironment.GetAbsolutePath(assemblyFolder.DirectoryPath)).AsParallel()
                    .Where(directoryPath => FileUtilities.DirectoryExistsNoThrow(directoryPath))
                    .SelectMany(
                        directoryPath =>
                            Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly))
                    .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// AssemblyfoldersEx object which contains the set of directories in assmblyfoldersFromConfig
        /// </summary>
        internal AssemblyFoldersFromConfig AssemblyFoldersFromConfig { get; }

        /// <summary>
        ///  Fast file exists for AssemblyFoldersFromConfig.
        /// </summary>
        internal bool FileExists(string path)
        {
            // Make sure that the file is in one of the directories under the assembly folders ex location
            // if it is not then we can not use this fast file existence check
            return _useOriginalFileExists ? _fileExists(path) : _filesInDirectories.Contains(path);
        }
    }
}
