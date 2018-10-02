// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// CoreCLR-compatible wrapper for loading task assemblies.
    /// </summary>
    internal sealed class CoreClrAssemblyLoader
    {
        private readonly Dictionary<string, Assembly> _pathsToAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Assembly> _namesToAssemblies = new Dictionary<string, Assembly>();
        private readonly HashSet<string> _dependencyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _guard = new object();

        private bool _resolvingHandlerHookedUp = false;

        private static readonly string[] _extensions = new[] { "ni.dll", "ni.exe", "dll", "exe" };
        private static readonly Version _currentAssemblyVersion = new Version(Microsoft.Build.Shared.MSBuildConstants.CurrentAssemblyVersion);
        private static readonly HashSet<string> _wellKnownAssemblyNames = new HashSet<string>(
            new[]
            {
                "Microsoft.Build",
                "Microsoft.Build.Framework",
                "Microsoft.Build.Tasks.Core",
                "Microsoft.Build.Utilities.Core"
            });

        public void AddDependencyLocation(string fullPath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            lock (_guard)
            {
                _dependencyPaths.Add(fullPath);
            }
        }

        public Assembly LoadFromPath(string fullPath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            Debug.Assert(Path.IsPathRooted(fullPath));

            lock (_guard)
            {
                if (!_resolvingHandlerHookedUp)
                {
                    AssemblyLoadContext.Default.Resolving += TryResolveAssembly;
                    _resolvingHandlerHookedUp = true;
                }

                Assembly assembly;
                if (_pathsToAssemblies.TryGetValue(fullPath, out assembly))
                {
                    return assembly;
                }

                return LoadAndCache(fullPath);
            }
        }

        private Assembly TryGetWellKnownAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            if (!_wellKnownAssemblyNames.Contains(assemblyName.Name))
            {
                return null;
            }

            assemblyName.Version = _currentAssemblyVersion;

            return context.LoadFromAssemblyName(assemblyName);
        }

        private Assembly TryResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            lock (_guard)
            {
                Assembly assembly = TryGetWellKnownAssembly(context, assemblyName);

                if (assembly != null)
                {
                    return assembly;
                }

                if (_namesToAssemblies.TryGetValue(assemblyName.FullName, out assembly))
                {
                    return assembly;
                }

                foreach (var cultureSubfolder in string.IsNullOrEmpty(assemblyName.CultureName)
                    // If no culture is specified, attempt to load directly from
                    // the known dependency paths.
                    ? new[] {string.Empty}
                    // Search for satellite assemblies in culture subdirectories
                    // of the assembly search directories, but fall back to the
                    // bare search directory if that fails.
                    : new[] {assemblyName.CultureName, string.Empty})
                {
                    foreach (var dependencyPath in _dependencyPaths)
                    {
                        foreach (var extension in _extensions)
                        {
                            var candidatePath = Path.Combine(dependencyPath,
                                cultureSubfolder,
                                $"{assemblyName.Name}.{extension}");
                            if (IsAssemblyAlreadyLoaded(candidatePath) ||
                                !FileSystems.Default.FileExists(candidatePath))
                            {
                                continue;
                            }

                            AssemblyName candidateAssemblyName = AssemblyLoadContext.GetAssemblyName(candidatePath);
                            if (candidateAssemblyName.Version != assemblyName.Version)
                            {
                                continue;
                            }

                            return LoadAndCache(candidatePath);
                        }
                    }
                }

                return null;
            }
        }

        /// <remarks>
        /// Assumes we have a lock on _guard
        /// </remarks>
        private Assembly LoadAndCache(string fullPath)
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            var name = assembly.FullName;

            _pathsToAssemblies[fullPath] = assembly;
            _namesToAssemblies[name] = assembly;

            return assembly;
        }

        private bool IsAssemblyAlreadyLoaded(string path)
        {
            return _pathsToAssemblies.ContainsKey(path);
        }
    }
}
