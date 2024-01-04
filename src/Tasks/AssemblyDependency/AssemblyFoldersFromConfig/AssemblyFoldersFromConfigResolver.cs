// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
using ProcessorArchitecture = System.Reflection.ProcessorArchitecture;

#nullable disable

namespace Microsoft.Build.Tasks.AssemblyFoldersFromConfig
{
    /// <summary>
    /// Resolve search path type {AssemblyFolderFromConfig: *}
    /// </summary>
    internal class AssemblyFoldersFromConfigResolver : Resolver
    {
        /// <summary>
        ///     Regex for breaking up the search path pieces.
        /// </summary>
        private static readonly Lazy<Regex> s_crackAssemblyFoldersFromConfigSentinel = new Lazy<Regex>(
            () => new Regex(
                AssemblyResolutionConstants.assemblyFoldersFromConfigSentinel +
                "(?<ASSEMBLYFOLDERCONFIGFILE>[^,]*),(?<TARGETRUNTIMEVERSION>[^,]*)}",
                RegexOptions.IgnoreCase | RegexOptions.Compiled));

        /// <summary>
        /// Whether or not the search path could be cracked.
        /// </summary>
        private bool _wasMatch;

        /// <summary>
        /// From the search path.
        /// </summary>
        private string _targetRuntimeVersion;

        /// <summary>
        /// Whether regex initialization has happened.
        /// </summary>
        private bool _isInitialized; // is initialized to false automatically

        /// <summary>
        /// List of assembly folders to search for keys in.
        /// </summary>
        private AssemblyFoldersFromConfigCache _assemblyFoldersCache;

        /// <summary>
        /// BuildEngine
        /// </summary>
        private readonly IBuildEngine4 _buildEngine;

        /// <summary>
        /// Task log context.
        /// </summary>
        private readonly TaskLoggingHelper _taskLogger;

        /// <summary>
        /// Path to the assembly folder config file.
        /// </summary>
        private string _assemblyFolderConfigFile;

        /// <summary>
        /// If it is not initialized then just return the null object, that would mean the resolver was not called.
        /// </summary>
        internal AssemblyFoldersFromConfig AssemblyFoldersExLocations => _assemblyFoldersCache?.AssemblyFoldersFromConfig;

        /// <summary>
        /// Construct.
        /// </summary>
        public AssemblyFoldersFromConfigResolver(string searchPathElement, GetAssemblyName getAssemblyName,
            FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion,
            ProcessorArchitecture targetProcessorArchitecture, bool compareProcessorArchitecture,
            IBuildEngine buildEngine, TaskLoggingHelper log)
            : base(
                searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion,
                targetProcessorArchitecture, compareProcessorArchitecture)
        {
            _buildEngine = buildEngine as IBuildEngine4;
            _taskLogger = log;
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
            Match match = s_crackAssemblyFoldersFromConfigSentinel.Value.Match(this.searchPathElement);
            _wasMatch = false;

            if (match.Success)
            {
                _targetRuntimeVersion = match.Groups["TARGETRUNTIMEVERSION"].Value.Trim();
                _assemblyFolderConfigFile = match.Groups["ASSEMBLYFOLDERCONFIGFILE"].Value.Trim();

                if (_targetRuntimeVersion.Length != 0)
                {
                    // Tolerate version keys that don't begin with "v" as these could come from user input
                    if (!_targetRuntimeVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        _targetRuntimeVersion = _targetRuntimeVersion.Insert(0, "v");
                    }

                    _wasMatch = true;

                    bool useCache = Environment.GetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE") == null;
                    string key = "6f7de854-47fe-4ae2-9cfe-9b33682abd91" + searchPathElement;

                    if (useCache && _buildEngine != null)
                    {
                        _assemblyFoldersCache = _buildEngine.GetRegisteredTaskObject(key, RegisteredTaskObjectLifetime.Build) as AssemblyFoldersFromConfigCache;
                    }

                    if (_assemblyFoldersCache == null)
                    {
                        // This should never happen. Microsoft.Common.CurrentVersion.targets will not specify a AssemblyFoldersFromConfig search path
                        // if the specified (or default) file is not found.
                        ErrorUtilities.VerifyThrow(FileSystems.Default.FileExists(_assemblyFolderConfigFile),
                            $"The AssemblyFolders config file specified does not exist: {_assemblyFolderConfigFile}");

                        try
                        {
                            AssemblyFoldersFromConfig assemblyFolders = new AssemblyFoldersFromConfig(_assemblyFolderConfigFile, _targetRuntimeVersion, targetProcessorArchitecture);
                            _assemblyFoldersCache = new AssemblyFoldersFromConfigCache(assemblyFolders, fileExists);
                            if (useCache)
                            {
                                _buildEngine?.RegisterTaskObject(key, _assemblyFoldersCache, RegisteredTaskObjectLifetime.Build, true /* dispose early ok*/);
                            }
                        }
                        catch (SerializationException e)
                        {
                            _taskLogger.LogError(ResourceUtilities.GetResourceString("ResolveAssemblyReference.AssemblyFoldersConfigFileMalformed"), _assemblyFolderConfigFile, e.Message);
                            return;
                        }
                    }

                    fileExists = _assemblyFoldersCache.FileExists;
                }
            }
        }

        /// <inheritdoc/>
        public override bool Resolve(
            AssemblyNameExtension assemblyName,
            string sdkName,
            string rawFileNameCandidate,
            bool isPrimaryProjectReference,
            bool isImmutableFrameworkReference,
            bool wantSpecificVersion,
            string[] executableExtensions,
            string hintPath,
            string assemblyFolderKey,
            List<ResolutionSearchLocation> assembliesConsideredAndRejected,
            out string foundPath,
            out bool userRequestedSpecificFile)
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
                        foreach (AssemblyFoldersFromConfigInfo assemblyFolder in _assemblyFoldersCache.AssemblyFoldersFromConfig)
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

                                // Lets see if the processor architecture matches, note this this method will cache the result when it was first called.
                                AssemblyNameExtension foundAssembly = getAssemblyName(candidatePath);

                                // If the processor architecture does not match then we should continue to see if there is a better match.
                                if (foundAssembly != null && (foundAssembly.AssemblyName.ProcessorArchitecture == ProcessorArchitecture.MSIL || foundAssembly.AssemblyName.ProcessorArchitecture == ProcessorArchitecture.None))
                                {
                                    foundPath = candidatePath;
                                    return true;
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
}
