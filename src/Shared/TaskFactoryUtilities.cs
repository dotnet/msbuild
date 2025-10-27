// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
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
    internal static class TaskFactoryUtilities
    {
        public const string InlineTaskSuffix = "inline_task.dll";
        public const string InlineTaskLoadManifestSuffix = ".loadmanifest";

        /// <summary>
        /// Represents a cached assembly entry for task factories with validation support.
        /// </summary>
        public readonly struct CachedAssemblyEntry
        {
            public CachedAssemblyEntry(Assembly assembly, string assemblyPath)
            {
                Assembly = assembly;
                AssemblyPath = assemblyPath;
            }

            public Assembly Assembly { get; }

            public string AssemblyPath { get; }

            /// <summary>
            /// Validates that the cached assembly is still usable.
            /// For out-of-process scenarios (when AssemblyPath is specified), validates the file exists.
            /// For in-process scenarios (when AssemblyPath is empty), always considers valid.
            /// </summary>
            public bool IsValid => string.IsNullOrEmpty(AssemblyPath) || FileUtilities.FileExistsNoThrow(AssemblyPath);
        }

        /// <summary>
        /// Gets a temporary file path for an inline task assembly in the process-specific directory.
        /// </summary>
        /// <returns>The full path to the temporary file.</returns>
        public static string GetTemporaryTaskAssemblyPath()
        {
            return FileUtilities.GetTemporaryFile(directory: null, fileName: null, extension: "inline_task.dll", createFile: false);
        }

        /// <summary>
        /// Creates a load manifest file containing directories that should be added to the assembly resolution path
        /// for out-of-process task execution.
        /// </summary>
        /// <param name="assemblyPath">The path to the task assembly.</param>
        /// <param name="directoriesToAdd">The list of directories to include in the manifest.</param>
        /// <returns>The path to the created manifest file.</returns>
        public static string CreateLoadManifest(string assemblyPath, List<string> directoriesToAdd)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));
            }

            if (directoriesToAdd == null)
            {
                throw new ArgumentNullException(nameof(directoriesToAdd));
            }

            string manifestPath = assemblyPath + InlineTaskLoadManifestSuffix;
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
        public static string? CreateLoadManifestFromReferences(string assemblyPath, List<string> referenceAssemblyPaths)
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
        /// <returns>A list of unique directory paths in order of first occurrence.</returns>
        public static List<string> ExtractUniqueDirectoriesFromAssemblyPaths(List<string> assemblyPaths)
        {
            if (assemblyPaths == null)
            {
                throw new ArgumentNullException(nameof(assemblyPaths));
            }

            var directories = new List<string>();
            var seenDirectories = new HashSet<string>(FileUtilities.PathComparer);

            foreach (string assemblyPath in assemblyPaths)
            {
                if (!string.IsNullOrEmpty(assemblyPath) && FileSystems.Default.FileExists(assemblyPath))
                {
                    string? directory = Path.GetDirectoryName(assemblyPath);
                    if (!string.IsNullOrEmpty(directory) && seenDirectories.Add(directory))
                    {
                        directories.Add(directory);
                    }
                }
            }

            return directories;
        }

        /// <summary>
        /// Loads an assembly from the specified path.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to load.</param>
        /// <returns>The loaded assembly.</returns>
        public static Assembly LoadTaskAssembly(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));
            }

            // Load the assembly from bytes so we don't lock the file and record its original path for out-of-proc hosts
            Assembly assembly = Assembly.Load(FileSystems.Default.ReadFileAllBytes(assemblyPath));
            return assembly;
        }


        /// <summary>
        /// Registers assembly resolution handlers for inline tasks based on their load manifest file.
        /// This enables out-of-process task execution to resolve dependencies that were identified
        /// during TaskFactory initialization.
        /// </summary>
        /// <param name="taskLocation">The path to the task assembly.</param>
        public static void RegisterAssemblyResolveHandlersFromManifest(string taskLocation)
        {
            if (string.IsNullOrEmpty(taskLocation))
            {
                throw new ArgumentException("Task location cannot be null or empty.", nameof(taskLocation));
            }

            if (!taskLocation.EndsWith(InlineTaskSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string manifestPath = taskLocation + InlineTaskLoadManifestSuffix;
            if (!FileSystems.Default.FileExists(manifestPath))
            {
                return;
            }

            string[] directories = File.ReadAllLines(manifestPath);
            if (directories?.Length > 0)
            {
                ResolveEventHandler resolver = CreateAssemblyResolver([.. directories]);
                AppDomain.CurrentDomain.AssemblyResolve += resolver;
            }
        }

        /// <summary>
        /// Creates an assembly resolution event handler that can resolve assemblies from a list of directories.
        /// This is typically used for in-memory compiled task assemblies that have custom reference locations.
        /// </summary>
        /// <param name="searchDirectories">The directories to search for assemblies.</param>
        /// <returns>A ResolveEventHandler that can be used with AppDomain.CurrentDomain.AssemblyResolve.</returns>
        public static ResolveEventHandler CreateAssemblyResolver(List<string> searchDirectories)
        {
            if (searchDirectories == null)
            {
                throw new ArgumentNullException(nameof(searchDirectories));
            }

            return (sender, args) => TryLoadAssembly(searchDirectories, new AssemblyName(args.Name));
        }

        /// <summary>
        /// Determines whether a task factory should compile for out-of-process execution based on the host context.
        /// </summary>
        /// <param name="taskFactoryEngineContext">The build engine/logging host passed to the task factory's Initialize method.</param>
        /// <returns>True if the task should be compiled for out-of-process execution; otherwise, false.</returns>
        /// <remarks>
        /// This method checks if the host implements ITaskFactoryBuildParameterProvider and queries it for:
        /// 1. ForceOutOfProcessExecution - explicit override via environment variable
        /// 2. IsMultiThreadedBuild - automatic out-of-proc when /mt flag is used
        /// 
        /// This logic is shared across RoslynCodeTaskFactory, CodeTaskFactory, and XamlTaskFactory.
        /// It needs to be decided during task factory initialization time.
        /// </remarks>
        public static bool ShouldCompileForOutOfProcess(IBuildEngine taskFactoryEngineContext)
        {
            if (taskFactoryEngineContext is ITaskFactoryBuildParameterProvider hostContext)
            {
                return hostContext.ForceOutOfProcessExecution || hostContext.IsMultiThreadedBuild;
            }

            return false;
        }

        /// <summary>
        /// Attempts to load an assembly by searching in the specified directories.
        /// </summary>
        /// <param name="directories">The directories to search in.</param>
        /// <param name="assemblyName">The name of the assembly to load.</param>
        /// <returns>The loaded assembly if found, otherwise null.</returns>
        private static Assembly? TryLoadAssembly(List<string> directories, AssemblyName assemblyName)
        {
            foreach (string directory in directories)
            {
                string path;

                // Try culture-specific path first if the assembly has a culture
                if (!string.IsNullOrEmpty(assemblyName.CultureName))
                {
                    path = Path.Combine(directory, assemblyName.CultureName, assemblyName.Name + ".dll");
                    if (FileSystems.Default.FileExists(path))
                    {
                        return Assembly.Load(FileSystems.Default.ReadFileAllBytes(path));
                    }
                }

                // Try the standard path
                path = Path.Combine(directory, assemblyName.Name + ".dll");
                if (FileSystems.Default.FileExists(path))
                {
                    return Assembly.Load(FileSystems.Default.ReadFileAllBytes(path));
                }
            }

            return null;
        }
    }
}
