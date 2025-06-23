// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Utilities class that provides common functionality for task factories such as
    /// temporary assembly creation and load manifest generation.
    ///
    /// This class consolidates duplicate logic that was previously scattered across:
    /// - RoslynCodeTaskFactory
    /// - CodeTaskFactory
    /// - XamlTaskFactory
    ///
    /// The common patterns include:
    /// 1. Creating process-specific temporary directories for inline task assemblies
    /// 2. Generating load manifest files for out-of-process task execution
    /// 3. Loading assemblies based on execution mode (in-process vs out-of-process)
    /// 4. Assembly resolution for custom reference locations
    /// </summary>
    public static class TaskFactoryUtilities
    {
        /// <summary>
        /// Creates a process-specific temporary directory for inline task assemblies.
        /// </summary>
        /// <returns>The path to the created temporary directory.</returns>
        public static string CreateProcessSpecificTaskDirectory()
        {
            string processSpecificInlineTaskDir = Path.Combine(
                FileUtilities.TempFileDirectory,
                MSBuildConstants.InlineTaskTempDllSubPath,
                $"pid_{EnvironmentUtilities.CurrentProcessId}");

            Directory.CreateDirectory(processSpecificInlineTaskDir);
            return processSpecificInlineTaskDir;
        }

        /// <summary>
        /// Gets a temporary file path for an inline task assembly in the process-specific directory.
        /// </summary>
        /// <param name="fileName">The base filename (without extension) to use. If null, a random name will be generated.</param>
        /// <param name="extension">The file extension to use (e.g., ".dll").</param>
        /// <returns>The full path to the temporary file.</returns>
        public static string GetTemporaryTaskAssemblyPath(string? fileName = null, string extension = ".dll")
        {
            string taskDir = CreateProcessSpecificTaskDirectory();
            return FileUtilities.GetTemporaryFile(taskDir, null, fileName ?? "inline_task" + extension, false);
        }

        /// <summary>
        /// Creates a load manifest file containing directories that should be added to the assembly resolution path
        /// for out-of-process task execution.
        /// </summary>
        /// <param name="assemblyPath">The path to the task assembly.</param>
        /// <param name="directoriesToAdd">The list of directories to include in the manifest.</param>
        /// <returns>The path to the created manifest file.</returns>
        public static string CreateLoadManifest(string assemblyPath, IEnumerable<string> directoriesToAdd)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));
            }

            if (directoriesToAdd == null)
            {
                throw new ArgumentNullException(nameof(directoriesToAdd));
            }

            string manifestPath = assemblyPath + ".loadmanifest";
            File.WriteAllLines(manifestPath, directoriesToAdd);
            return manifestPath;
        }

        /// <summary>
        /// Creates a load manifest file from reference assembly paths by extracting their directories.
        /// This is a convenience method that extracts unique directories from assembly paths and creates the manifest.
        /// </summary>
        /// <param name="assemblyPath">The path to the task assembly.</param>
        /// <param name="referenceAssemblyPaths">The list of reference assembly paths to extract directories from.</param>
        /// <returns>The path to the created manifest file, or null if no valid directories were found.</returns>
        public static string? CreateLoadManifestFromReferences(string assemblyPath, IEnumerable<string> referenceAssemblyPaths)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));
            }

            if (referenceAssemblyPaths == null)
            {
                throw new ArgumentNullException(nameof(referenceAssemblyPaths));
            }

            var directories = ExtractUniqueDirectoriesFromAssemblyPaths(referenceAssemblyPaths);

            if (directories.Count == 0)
            {
                return null;
            }

            return CreateLoadManifest(assemblyPath, directories);
        }

        /// <summary>
        /// Extracts unique directories from a collection of assembly file paths.
        /// Only includes directories for assemblies that actually exist on disk.
        /// </summary>
        /// <param name="assemblyPaths">The collection of assembly file paths.</param>
        /// <returns>A list of unique directory paths.</returns>
        public static IList<string> ExtractUniqueDirectoriesFromAssemblyPaths(IEnumerable<string> assemblyPaths)
        {
            if (assemblyPaths == null)
            {
                throw new ArgumentNullException(nameof(assemblyPaths));
            }

            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string assemblyPath in assemblyPaths)
            {
                if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
                {
                    string? directory = Path.GetDirectoryName(assemblyPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        directories.Add(directory);
                    }
                }
            }

            return directories.ToList();
        }

        /// <summary>
        /// Loads an assembly based on whether out-of-process execution is enabled.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to load.</param>
        /// <param name="useOutOfProcess">Whether to use out-of-process execution mode.</param>
        /// <returns>The loaded assembly.</returns>
        public static Assembly LoadTaskAssembly(string assemblyPath, bool useOutOfProcess)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));
            }

            if (useOutOfProcess)
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            else
            {
                return Assembly.Load(File.ReadAllBytes(assemblyPath));
            }
        }

        /// <summary>
        /// Creates an assembly resolution event handler that can resolve assemblies from a list of directories.
        /// This is typically used for in-memory compiled task assemblies that have custom reference locations.
        /// </summary>
        /// <param name="searchDirectories">The directories to search for assemblies.</param>
        /// <returns>A ResolveEventHandler that can be used with AppDomain.CurrentDomain.AssemblyResolve.</returns>
        public static ResolveEventHandler CreateAssemblyResolver(IList<string> searchDirectories)
        {
            if (searchDirectories == null)
            {
                throw new ArgumentNullException(nameof(searchDirectories));
            }

            return (sender, args) => TryLoadAssembly(searchDirectories, new AssemblyName(args.Name));
        }

        /// <summary>
        /// Attempts to load an assembly by searching in the specified directories.
        /// </summary>
        /// <param name="directories">The directories to search in.</param>
        /// <param name="assemblyName">The name of the assembly to load.</param>
        /// <returns>The loaded assembly if found, otherwise null.</returns>
        private static Assembly? TryLoadAssembly(IList<string> directories, AssemblyName assemblyName)
        {
            foreach (string directory in directories)
            {
                string path;

                // Try culture-specific path first if the assembly has a culture
                if (!string.IsNullOrEmpty(assemblyName.CultureName))
                {
                    path = Path.Combine(directory, assemblyName.CultureName, assemblyName.Name + ".dll");
                    if (File.Exists(path))
                    {
                        return Assembly.LoadFrom(path);
                    }
                }

                // Try the standard path
                path = Path.Combine(directory, assemblyName.Name + ".dll");
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
            }

            return null;
        }
    }
}
