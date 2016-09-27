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
    /// <remarks>
    /// This type defines a new <see cref="AssemblyLoadContext"/>. This isn't really what we want to
    /// do, as we aren't trying to isolate our assembly loads from the default context. All we want
    /// to do is add support for loading task assemblies and their dependencies, which we assume are
    /// located immediately next to them in the file system. Ideally we wouldn't derive from
    /// <see cref="AssemblyLoadContext"/> at all, but simply hook the <see cref="AssemblyLoadContext.Resolving"/>
    /// event to handle finding the dependencies. For the moment, however, that approach is blocked
    /// by https://github.com/dotnet/coreclr/issues/5837.
    /// </remarks>
    internal sealed class CoreClrAssemblyLoader : AssemblyLoadContext
    {
        private readonly Dictionary<string, Assembly> _pathsToAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Assembly> _namesToAssemblies = new Dictionary<string, Assembly>();
        private readonly HashSet<string> _dependencyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _guard = new object();

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
                Assembly assembly;
                if (_pathsToAssemblies.TryGetValue(fullPath, out assembly))
                {
                    return assembly;
                }

                return LoadAndCache(fullPath);
            }
        }

        private static readonly string[] s_extensions = new string[] { ".dll", ".exe" };

        /// <summary>
        /// Searches and loads from the base directory of the current
        /// app context
        /// </summary>
        private Assembly AppContextLoad(AssemblyName assemblyName)
        {
            var baseDir = AppContext.BaseDirectory;
            foreach (var extension in s_extensions)
            {
                var path = Path.Combine(baseDir, assemblyName.Name + extension);

                if (File.Exists(path))
                {
                    lock (_guard)
                    {
                        return LoadAndCache(path);
                    }
                }
            }
            return null;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            lock (_guard)
            {
                // First try to satisfy the load from the default context.
                Assembly assembly;
                try
                {
                    assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
                    if (assembly != null)
                    {
                        return assembly;
                    }
                }
                catch
                {
                    // LoadFromAssemblyName indicates failure by throwing exceptions; we need to
                    // catch them, ignore them, and try to load the assembly ourselves.
                    // This may mask underlying issues where DefaultContext.LoadFromAssemblyName
                    // should have succeeded, but there isn't much we can do about that.
                }

                // Try and grab assembly using standard load
                assembly = AppContextLoad(assemblyName);
                if (assembly != null)
                {
                    return assembly;
                }

                string fullName = assemblyName.FullName;

                if (_namesToAssemblies.TryGetValue(fullName, out assembly))
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
            var assembly = LoadFromAssemblyPath(fullPath);
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
