// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;


//using Roslyn.Utilities;

//using static Microsoft.CodeAnalysis.AnalyzerAssemblyLoadUtils;

namespace Microsoft.Build.Shared
{
    /// Core CLR compatible wrapper for loading analyzers.
    internal sealed class CoreClrAssemblyLoader : AssemblyLoadContext
    {
        private readonly Dictionary<string, Assembly> _pathsToAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Assembly> _namesToAssemblies = new Dictionary<string, Assembly>();
        //private readonly List<string> _dependencyPaths = new List<string>();
        private readonly object _guard = new object();

        /// <summary>
        /// Creates a new instance of <see cref="CoreClrAssemblyLoader" />,
        /// sets that instance to be the default <see cref="AssemblyLoadContext" />,
        /// and returns that instance. Throws if the Default is already set or the
        /// binding model is already locked.
        /// </summary>
        public static CoreClrAssemblyLoader CreateAndSetDefault()
        {
            var assemblyLoader = new CoreClrAssemblyLoader();
            return assemblyLoader;
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
                // Try and grab assembly using standard load
                Assembly assembly = AppContextLoad(assemblyName);
                if (assembly != null)
                {
                    return assembly;
                }

                string fullName = assemblyName.FullName;

                if (_namesToAssemblies.TryGetValue(fullName, out assembly))
                {
                    return assembly;
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
    }
}
