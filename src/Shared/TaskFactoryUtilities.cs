// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

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
    /// 5. Tracking assembly paths for inline tasks loaded from bytes
    /// </summary>
    internal static class TaskFactoryUtilities
    {
        /// <summary>
        /// Registry to track the mapping between assembly identity and the original file path.
        /// This is needed because when assemblies are loaded from bytes, Assembly.Location is empty.
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> s_assemblyPathRegistry = new();

        /// <summary>
        /// The sub-path within the temporary directory where compiled inline tasks are located.
        /// </summary>
        public const string InlineTaskTempDllSubPath = nameof(InlineTaskTempDllSubPath);
        public const string InlineTaskSuffix = "inline_task.dll";
        public const string InlineTaskLoadManifestSuffix = ".loadmanifest";


        /// <summary>
        /// Creates a process-specific temporary directory for inline task assemblies.
        /// </summary>
        /// <returns>The path to the created temporary directory.</returns>
        public static string CreateProcessSpecificTemporaryTaskDirectory()
        {
            string processSpecificInlineTaskDir = Path.Combine(
                FileUtilities.TempFileDirectory,
                InlineTaskTempDllSubPath,
                $"pid_{EnvironmentUtilities.CurrentProcessId}");

            Directory.CreateDirectory(processSpecificInlineTaskDir);
            return processSpecificInlineTaskDir;
        }

        /// <summary>
        /// Gets a temporary file path for an inline task assembly in the process-specific directory.
        /// </summary>
        /// <returns>The full path to the temporary file.</returns>
        public static string GetTemporaryTaskAssemblyPath()
        {
            string taskDir = CreateProcessSpecificTemporaryTaskDirectory();
            return FileUtilities.GetTemporaryFile(taskDir, fileName: null, extension: "inline_task.dll", createFile: false);
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
                if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
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

            Assembly assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            
            // Register the mapping between assembly identity and file path
            RegisterAssemblyPath(assembly, assemblyPath);
            
            return assembly;
        }

        /// <summary>
        /// Registers the mapping between an assembly and its original file path.
        /// This is essential for assemblies loaded from bytes where Assembly.Location is empty.
        /// </summary>
        /// <param name="assembly">The loaded assembly.</param>
        /// <param name="assemblyPath">The original file path of the assembly.</param>
        public static void RegisterAssemblyPath(Assembly assembly, string assemblyPath)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));
            }

            // Use assembly full name as the key for uniqueness
            string assemblyKey = assembly.FullName ?? assembly.GetName().Name ?? "Unknown";
            s_assemblyPathRegistry.TryAdd(assemblyKey, assemblyPath);
        }

        /// <summary>
        /// Attempts to get the registered assembly path for the given assembly.
        /// </summary>
        /// <param name="assembly">The assembly to look up.</param>
        /// <param name="assemblyPath">The registered assembly path if found.</param>
        /// <returns>True if the assembly path was found; otherwise, false.</returns>
        public static bool TryGetRegisteredAssemblyPath(Assembly assembly, out string assemblyPath)
        {
            assemblyPath = string.Empty;

            if (assembly == null)
            {
                return false;
            }

            string assemblyKey = assembly.FullName ?? assembly.GetName().Name ?? "Unknown";
            if (s_assemblyPathRegistry.TryGetValue(assemblyKey, out string? registeredPath))
            {
                assemblyPath = registeredPath;
                return true;
            }
            
            return false;
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
            if (!File.Exists(manifestPath))
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
        /// Cleans up the current process's inline task directory by deleting the temporary directory
        /// and its contents used for inline task assemblies for this specific process.
        /// This should be called at the end of a build to prevent dangling DLL files.
        /// </summary>
        /// <remarks>
        /// On Windows platforms, this may fail to delete files that are still locked by the current process.
        /// However, it will clean up any files that are no longer in use.
        /// </remarks>
        public static void CleanCurrentProcessInlineTaskDirectory()
        {
            string processSpecificInlineTaskDir = Path.Combine(
                FileUtilities.TempFileDirectory,
                InlineTaskTempDllSubPath,
                $"pid_{EnvironmentUtilities.CurrentProcessId}");
                
            if (Directory.Exists(processSpecificInlineTaskDir))
            {
                FileUtilities.DeleteDirectoryNoThrow(processSpecificInlineTaskDir, recursive: true);
            }
        }

        /// <summary>
        /// Clears the assembly path registry. This should be called at the end of a build
        /// to prevent memory leaks and stale assembly path references.
        /// </summary>
        public static void ClearAssemblyPathRegistry()
        {
            s_assemblyPathRegistry.Clear();
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
                    if (File.Exists(path))
                    {
                        return Assembly.Load(File.ReadAllBytes(path));
                    }
                }

                // Try the standard path
                path = Path.Combine(directory, assemblyName.Name + ".dll");
                if (File.Exists(path))
                {
                    return Assembly.Load(File.ReadAllBytes(path));
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to reconstruct the assembly path for inline tasks loaded from bytes.
        /// When assemblies are loaded via Assembly.Load(byte[]), their Location property is empty,
        /// but we need the original path for AssemblyLoadInfo creation.
        /// </summary>
        /// <param name="taskType">The task type whose assembly path needs reconstruction.</param>
        /// <param name="assemblyPath">When this method returns true, contains the reconstructed assembly path.</param>
        /// <returns>True if the assembly path was successfully reconstructed; otherwise, false.</returns>
        public static bool TryReconstructInlineTaskAssemblyPath(Type taskType, out string assemblyPath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskType, nameof(taskType));
            
            assemblyPath = string.Empty;
            Assembly assembly = taskType.Assembly;
            
            // If assembly has a location, we're done
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                assemblyPath = assembly.Location;
                return true;
            }

            // Try to get the registered assembly path
            if (TryGetRegisteredAssemblyPath(assembly, out assemblyPath))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the process-specific inline task directory path.
        /// </summary>
        /// <returns>The directory path for the current process's inline tasks.</returns>
        private static string GetProcessSpecificInlineTaskDirectory()
        {
            return Path.Combine(
                FileUtilities.TempFileDirectory,
                InlineTaskTempDllSubPath,
                $"pid_{EnvironmentUtilities.CurrentProcessId}");
        }
    }
}
