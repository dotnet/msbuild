// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if FEATURE_WIN32_REGISTRY

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using ProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {Registry: *}
    /// </summary>
    internal class AssemblyFoldersExResolver : Resolver
    {
        /// <summary>
        /// Regex for breaking up the searchpath pieces.
        /// </summary>
        private static readonly Lazy<Regex> s_crackAssemblyFoldersExSentinel = new Lazy<Regex>(
            () => new Regex
                (
                AssemblyResolutionConstants.assemblyFoldersExSentinel +
                "(?<REGISTRYKEYROOT>[^,]*),(?<TARGETRUNTIMEVERSION>[^,]*),(?<REGISTRYKEYSUFFIX>[^,]*)([,]*)(?<CONDITIONS>.*)}",
                RegexOptions.IgnoreCase | RegexOptions.Compiled
                )
            );

        /// <summary>
        /// Delegate.
        /// </summary>
        private readonly GetRegistrySubKeyNames _getRegistrySubKeyNames;

        /// <summary>
        /// Delegate
        /// </summary>
        private readonly GetRegistrySubKeyDefaultValue _getRegistrySubKeyDefaultValue;

        /// <summary>
        /// Open the base registry key given a hive and a view
        /// </summary>
        private readonly OpenBaseKey _openBaseKey;

        /// <summary>
        /// Whether or not the search path could be cracked.
        /// </summary>
        private bool _wasMatch;

        /// <summary>
        /// From the search path.
        /// </summary>
        private string _registryKeyRoot;

        /// <summary>
        /// From the search path.
        /// </summary>
        private string _targetRuntimeVersion;

        /// <summary>
        /// From the search path.
        /// </summary>
        private string _registryKeySuffix;

        /// <summary>
        /// From the search path.
        /// </summary>
        private string _osVersion;

        /// <summary>
        /// From the search path.
        /// </summary>
        private string _platform;

        /// <summary>
        /// Whether regex initialization has happened.
        /// </summary>
        private bool _isInitialized; // is initialized to false automatically

        /// <summary>
        /// List of assembly folders to search for keys in.
        /// </summary>
        private AssemblyFoldersExCache _assemblyFoldersCache;

        /// <summary>
        /// BuildEngine
        /// </summary>
        private readonly IBuildEngine4 _buildEngine;

        /// <summary>
        /// If it is not initialized then just return the null object, that would mean the resolver was not called.
        /// </summary>
        internal AssemblyFoldersEx AssemblyFoldersExLocations => _assemblyFoldersCache?.AssemblyFoldersEx;

        /// <summary>
        /// Construct.
        /// </summary>
        public AssemblyFoldersExResolver(string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetRegistrySubKeyNames getRegistrySubKeyNames, GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue, GetAssemblyRuntimeVersion getRuntimeVersion, OpenBaseKey openBaseKey, Version targetedRuntimeVesion, ProcessorArchitecture targetProcessorArchitecture, bool compareProcessorArchitecture, IBuildEngine buildEngine)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, targetProcessorArchitecture, compareProcessorArchitecture)
        {
            _buildEngine = buildEngine as IBuildEngine4;
            _getRegistrySubKeyNames = getRegistrySubKeyNames;
            _getRegistrySubKeyDefaultValue = getRegistrySubKeyDefaultValue;
            _openBaseKey = openBaseKey;
        }

        /// <summary>
        /// Initialize this class if it hasn't been initialized yet.
        /// </summary>
        private void LazyInitialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            // Crack the search path just one time.
            Match match = s_crackAssemblyFoldersExSentinel.Value.Match(this.searchPathElement);
            _wasMatch = false;

            if (match.Success)
            {
                _registryKeyRoot = match.Groups["REGISTRYKEYROOT"].Value.Trim();
                _targetRuntimeVersion = match.Groups["TARGETRUNTIMEVERSION"].Value.Trim();
                _registryKeySuffix = match.Groups["REGISTRYKEYSUFFIX"].Value.Trim();
                _osVersion = null;
                _platform = null;
                Group conditions = match.Groups["CONDITIONS"];

                // Disregard if there are any empty values in the {Registry} tag.
                if (_registryKeyRoot.Length != 0 && _targetRuntimeVersion.Length != 0 && _registryKeySuffix.Length != 0)
                {
                    // Tolerate version keys that don't begin with "v" as these could come from user input
                    if (!_targetRuntimeVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        _targetRuntimeVersion = _targetRuntimeVersion.Insert(0, "v");
                    }

                    if (conditions?.Value != null && conditions.Length > 0 && conditions.Value.Length > 0)
                    {
                        string value = conditions.Value.Trim();

                        // Parse the condition statement for OSVersion and Platform
                        foreach (string c in value.Split(':'))
                        {
                            if (String.Compare(c, 0, "OSVERSION=", 0, 10, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                _osVersion = c.Substring(10);
                            }
                            else if (String.Compare(c, 0, "PLATFORM=", 0, 9, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                _platform = c.Substring(9);
                            }
                        }
                    }
                    _wasMatch = true;

                    bool useCache = Environment.GetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE") == null;
                    string key = "ca22615d-aa83-444b-80b9-b32f3d5db097" + this.searchPathElement;
                    if (useCache && _buildEngine != null)
                    {
                        _assemblyFoldersCache = _buildEngine.GetRegisteredTaskObject(key, RegisteredTaskObjectLifetime.Build) as AssemblyFoldersExCache;
                    }

                    if (_assemblyFoldersCache == null)
                    {
                        AssemblyFoldersEx assemblyFolders = new AssemblyFoldersEx(_registryKeyRoot, _targetRuntimeVersion, _registryKeySuffix, _osVersion, _platform, _getRegistrySubKeyNames, _getRegistrySubKeyDefaultValue, this.targetProcessorArchitecture, _openBaseKey);
                        _assemblyFoldersCache = new AssemblyFoldersExCache(assemblyFolders, fileExists);
                        if (useCache)
                        {
                            _buildEngine?.RegisterTaskObject(key, _assemblyFoldersCache, RegisteredTaskObjectLifetime.Build, true /* dispose early ok*/);
                        }
                    }

                    fileExists = _assemblyFoldersCache.FileExists;
                }
            }
        }

        /// <summary>
        /// Resolve a reference to a specific file name.
        /// </summary>
        /// <param name="assemblyName">The assemblyname of the reference.</param>
        /// <param name="rawFileNameCandidate">The reference's 'include' treated as a raw file name.</param>
        /// <param name="isPrimaryProjectReference">Whether or not this reference was directly from the project file (and therefore not a dependency)</param>
        /// <param name="wantSpecificVersion">Whether an exact version match is requested.</param>
        /// <param name="executableExtensions">Allowed executable extensions.</param>
        /// <param name="hintPath">The item's hintpath value.</param>
        /// <param name="assemblyFolderKey">Like "hklm\Vendor RegKey" as provided to a reference by the &lt;AssemblyFolderKey&gt; on the reference in the project.</param>
        /// <param name="assembliesConsideredAndRejected">Receives the list of locations that this function tried to find the assembly. May be "null".</param>
        /// <param name="foundPath">The path where the file was found.</param>
        /// <param name="userRequestedSpecificFile">Whether or not the user wanted a specific file (for example, HintPath is a request for a specific file)</param>
        /// <returns>True if the file was resolved.</returns>
        public override bool Resolve
        (
            AssemblyNameExtension assemblyName,
            string sdkName,
            string rawFileNameCandidate,
            bool isPrimaryProjectReference,
            bool wantSpecificVersion,
            string[] executableExtensions,
            string hintPath,
            string assemblyFolderKey,
            ArrayList assembliesConsideredAndRejected,
            out string foundPath,
            out bool userRequestedSpecificFile
        )
        {
            foundPath = null;
            userRequestedSpecificFile = false;

            if (assemblyName != null)
            {
                LazyInitialize();

                if (_wasMatch)
                {
                    string resolvedPath = null;
                    if (_assemblyFoldersCache != null)
                    {
                        foreach (AssemblyFoldersExInfo assemblyFolder in _assemblyFoldersCache.AssemblyFoldersEx)
                        {
                            string candidatePath = ResolveFromDirectory(assemblyName, isPrimaryProjectReference, wantSpecificVersion, executableExtensions, assemblyFolder.DirectoryPath, assembliesConsideredAndRejected);

                            // We have a full path returned 
                            if (candidatePath != null)
                            {
                                if (resolvedPath == null)
                                {
                                    resolvedPath = candidatePath;
                                }

                                // We are not targeting MSIL thus we must have a match because ResolveFromDirectory only will return a match if we find an assembly matching the targeted processor architecture
                                if (targetProcessorArchitecture != ProcessorArchitecture.MSIL && targetProcessorArchitecture != ProcessorArchitecture.None)
                                {
                                    foundPath = candidatePath;
                                    return true;
                                }
                                else
                                {
                                    // Lets see if the processor architecture matches, note this this method will cache the result when it was first called.
                                    AssemblyNameExtension foundAssembly = getAssemblyName(candidatePath);

                                    // If the processor architecture does not match the we should continue to see if there is a better match.
                                    if (foundAssembly != null && (foundAssembly.AssemblyName.ProcessorArchitecture == ProcessorArchitecture.MSIL || foundAssembly.AssemblyName.ProcessorArchitecture == ProcessorArchitecture.None))
                                    {
                                        foundPath = candidatePath;
                                        return true;
                                    }
                                }
                            }
                        }
                    }

                    // If we get to this point and have not returned then we have the best assembly we could find, lets return it.
                    if (resolvedPath != null)
                    {
                        foundPath = resolvedPath;
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Contains information about entries in the AssemblyFoldersEx registry keys.
    /// </summary>
    internal class AssemblyFoldersExCache
    {
        /// <summary>
        /// Set of files in ALL assemblyfoldersEx directories
        /// </summary>
        private readonly HashSet<string> _filesInDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        internal AssemblyFoldersExCache(AssemblyFoldersEx assemblyFoldersEx, FileExists fileExists)
        {
            AssemblyFoldersEx = assemblyFoldersEx;
            _fileExists = fileExists;

            if (Environment.GetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE") != null)
            {
                _useOriginalFileExists = true;
            }
            else
            {
                var lockobject = new Object();

                Parallel.ForEach(assemblyFoldersEx, assemblyFolder =>
                {
                    if (FileUtilities.DirectoryExistsNoThrow(assemblyFolder.DirectoryPath))
                    {
                        string[] files = Directory.GetFiles(assemblyFolder.DirectoryPath, "*.*", SearchOption.TopDirectoryOnly);

                        lock (lockobject)
                        {
                            foreach (string file in files)
                            {
                                _filesInDirectories.Add(file);
                            }
                        }
                    }
                });
            }
        }

        /// <summary>
        /// AssemblyfoldersEx object which contains the set of directories in assmblyfoldersex
        /// </summary>
        internal AssemblyFoldersEx AssemblyFoldersEx { get; }

        /// <summary>
        ///  Fast file exists for assemblyfoldersex.
        /// </summary>
        internal bool FileExists(string path)
        {
            // Make sure that the file is in one of the directories under the assembly folders ex location
            // if it is not then we can not use this fast file existence check
            if (!_useOriginalFileExists)
            {
                bool exists = _filesInDirectories.Contains(path);
                return exists;
            }
            return _fileExists(path);
        }
    }
}
#endif
