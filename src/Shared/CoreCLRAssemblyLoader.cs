// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

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

        private Assembly TryResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            lock (_guard)
            {
                Assembly assembly;
                if (_namesToAssemblies.TryGetValue(assemblyName.FullName, out assembly))
                {
                    return assembly;
                }

                foreach (var dependencyPath in _dependencyPaths)
                {
                    foreach (var extension in _extensions)
                    {
                        var candidatePath = Path.Combine(dependencyPath, $"{assemblyName.Name}.{extension}");
                        if (IsAssemblyAlreadyLoaded(candidatePath) ||
                            !File.Exists(candidatePath))
                        {
                            continue;
                        }

                        return LoadAndCache(candidatePath);
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
