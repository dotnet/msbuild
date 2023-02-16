// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
#if !RUNTIME_TYPE_NETCORE
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
#endif

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     Attempts to classify project files for various purposes such as safety and performance.
    /// </summary>
    /// <remarks>
    ///     Callers of this class are responsible to respect current OS path string comparison.
    ///     <para>
    ///         The term "project files" refers to the root project file (e.g. <c>MyProject.csproj</c>) and
    ///         any other <c>.props</c> and <c>.targets</c> files it imports.
    ///     </para>
    ///     <para>
    ///         Classifications provided are:
    ///         <list type="number">
    ///             <item>
    ///                 <see cref="IsNonModifiable" /> which indicates the file is not expected to change over time,
    ///                 other than when it is first created. This is a subset of non-user-editable files and
    ///                 generally excludes generated files which can be regenerated in response to user actions.
    ///             </item>
    ///         </list>
    ///     </para>
    /// </remarks>
    internal class FileClassifier
    {
        /// <summary>
        ///     StringComparison used for comparing paths on current OS.
        /// </summary>
        /// <remarks>
        ///     TODO: Replace RuntimeInformation.IsOSPlatform(OSPlatform.Linux) by NativeMethodsShared.OSUsesCaseSensitivePaths once it is moved out from Shared
        /// </remarks>
        private static readonly StringComparison PathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        /// <summary>
        ///     Single, static <see cref="Lazy{T}"/> instance of shared file FileClassifier for <see cref="Shared"/> member.
        /// </summary>
        private static readonly Lazy<FileClassifier> s_sharedInstance = new(() => new FileClassifier());

        /// <summary>
        ///     Serves purpose of thread safe set of known immutable directories.
        /// </summary>
        /// <remarks>
        ///     Although <see cref="ConcurrentDictionary{TKey,TValue}"></see> is not optimal memory-wise, in this particular case it does not matter
        ///     much as the expected size of this set is ~5 and in very extreme cases less then 100.
        /// </remarks>
        private readonly ConcurrentDictionary<string, string> _knownImmutableDirectories = new();

        /// <summary>
        ///     Copy on write snapshot of <see cref="_knownImmutableDirectories"/>.
        /// </summary>
        private volatile IReadOnlyList<string> _knownImmutableDirectoriesSnapshot = Array.Empty<string>();

        /// <summary>
        ///     Creates default FileClassifier which following immutable folders:
        ///     Classifications provided are:
        ///     <list type="number">
        ///         <item>Program Files\Reference Assemblies\Microsoft</item>
        ///         <item>Program Files (x86)\Reference Assemblies\Microsoft</item>
        ///         <item>Visual Studio installation root</item>
        ///     </list>
        /// </summary>
        /// <remarks>
        ///     Individual projects NuGet folders are added during project build by calling
        ///     <see cref="RegisterImmutableDirectory" />
        /// </remarks>
        public FileClassifier()
        {
            // Register Microsoft "Reference Assemblies" as immutable
            string[] programFilesEnvs = new[] { "ProgramFiles(x86)", "ProgramW6432", "ProgramFiles(Arm)" };
            foreach (string programFilesEnv in programFilesEnvs)
            {
                string? programFiles = Environment.GetEnvironmentVariable(programFilesEnv);
                if (!string.IsNullOrEmpty(programFiles))
                {
                    RegisterImmutableDirectory(Path.Combine(programFiles, "Reference Assemblies", "Microsoft"));
                }
            }

#if !RUNTIME_TYPE_NETCORE
            RegisterImmutableDirectory(GetVSInstallationDirectory());

            static string? GetVSInstallationDirectory()
            {
                string? dir = Environment.GetEnvironmentVariable("VSAPPIDDIR");

                if (dir != null)
                {
                    // The path provided is not the installation root, but rather the location of devenv.exe.
                    // __VSSPROPID.VSSPROPID_InstallDirectory has the same value.
                    // Failing a better way to obtain the installation root, remove that suffix.
                    // Obviously this is brittle against changes to the relative path of devenv.exe, however that seems
                    // unlikely and should be easy to work around if ever needed.
                    const string devEnvExeRelativePath = "Common7\\IDE\\";

                    if (dir.EndsWith(devEnvExeRelativePath, PathComparison))
                    {
                        dir = dir.Substring(0, dir.Length - devEnvExeRelativePath.Length);

                        return dir;
                    }
                }

                // TODO: Once BuildEnvironmentHelper makes it from Shared into Framework, rework the code bellow. Hint: implement GetVsRootFromMSBuildAssembly() in BuildEnvironmentHelper

                // Seems like MSBuild did not run from VS but from CLI.
                // Identify current process and run it
                string processName = Process.GetCurrentProcess().MainModule.FileName;
                string processFileName = Path.GetFileNameWithoutExtension(processName);

                if (string.IsNullOrEmpty(processFileName))
                {
                    return null;
                }

                string[] msBuildProcess = { "MSBUILD", "MSBUILDTASKHOST" };
                if (msBuildProcess.Any(s =>
                    processFileName.Equals(s, StringComparison.OrdinalIgnoreCase)))
                {
                    // Check if we're in a VS installation
                    if (Regex.IsMatch(processName, $@".*\\MSBuild\\Current\\Bin\\.*MSBuild(?:TaskHost)?\.exe", RegexOptions.IgnoreCase))
                    {
                        return GetVsRootFromMSBuildAssembly(processName);
                    }
                }

                return null;

                static string GetVsRootFromMSBuildAssembly(string msBuildAssembly)
                {
                    return GetFolderAbove(msBuildAssembly,
                        Path.GetDirectoryName(msBuildAssembly)?.EndsWith(@"\amd64", StringComparison.OrdinalIgnoreCase) == true
                            ? 5
                            : 4);
                }

                static string GetFolderAbove(string path, int count = 1)
                {
                    if (count < 1)
                    {
                        return path;
                    }

                    DirectoryInfo? parent = Directory.GetParent(path);

                    while (count > 1 && parent?.Parent != null)
                    {
                        parent = parent.Parent;
                        count--;
                    }

                    return parent?.FullName ?? path;
                }
            }
#endif
        }

        /// <summary>
        ///     Shared singleton instance.
        /// </summary>
        public static FileClassifier Shared => s_sharedInstance.Value;

        /// <summary>
        ///     Try add path into set of known immutable paths.
        ///     Files under any of these folders are considered non-modifiable.
        /// </summary>
        /// <remarks>
        ///     This value is used by <see cref="IsNonModifiable" />.
        ///     Files in the NuGet package cache are not expected to change over time, once they are created.
        /// </remarks>
        public void RegisterImmutableDirectory(string? directory)
        {
            if (directory?.Length > 0)
            {
                string d = EnsureTrailingSlash(directory);

                if (_knownImmutableDirectories.TryAdd(d, d))
                {
                    _knownImmutableDirectoriesSnapshot = new List<string>(_knownImmutableDirectories.Values);
                }
            }
        }

        private static string EnsureTrailingSlash(string fileSpec)
        {
            if (fileSpec.Length >= 1)
            {
                char lastChar = fileSpec[fileSpec.Length - 1];
                if (lastChar != Path.DirectorySeparatorChar && lastChar != Path.AltDirectorySeparatorChar)
                {
                    fileSpec += Path.DirectorySeparatorChar;
                }
            }

            return fileSpec;
        }

        /// <summary>
        ///     Gets whether a file is expected to not be modified in place on disk once it has been created.
        /// </summary>
        /// <param name="filePath">The path to the file to test.</param>
        /// <returns><see langword="true" /> if the file is non-modifiable, otherwise <see langword="false" />.</returns>
        public bool IsNonModifiable(string filePath)
        {
            // Avoid a foreach loop or linq.Any because they allocate.
            // Copy _knownImmutableDirectoriesSnapshot into a local variable so other threads can't modify it during enumeration.
            IReadOnlyList<string> immutableDirectories = _knownImmutableDirectoriesSnapshot;
            for (int i = 0; i < immutableDirectories.Count; i++)
            {
                if (filePath.StartsWith(immutableDirectories[i], PathComparison))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
