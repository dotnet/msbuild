// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     Attempts to classify project files for various purposes such as safety and performance.
    /// </summary>
    /// <remarks>
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
        private const StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        ///     Single, static instance of an array that contains a semi-colon ';', which is used to split strings.
        /// </summary>
        private static readonly char[] s_semicolonDelimiter = {';'};

        /// <summary>
        ///     Singe, static <see cref="Lazy{T}"/> instance of shared file FileClassifier for <see cref="Shared"/> member.
        /// </summary>
        private static readonly Lazy<FileClassifier> s_sharedInstance = new(() => new FileClassifier());

        private readonly ConcurrentDictionary<string, string> _knownImmutableDirectory = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Creates default FileClassifier which following immutable folders:
        ///     Classifications provided are:
        ///     <list type="number">
        ///         <item>Program Files</item>
        ///         <item>Program Files (x86)</item>
        ///         <item>Default .nuget cache location</item>
        ///         <item>Visual Studio installation root</item>
        ///     </list>
        /// </summary>
        /// <remarks>
        ///     Individual projects NuGet folders are added during project build by calling
        ///     <see cref="RegisterNuGetPackageFolders" />
        /// </remarks>
        public FileClassifier()
        {
            RegisterImmutableDirectory(Environment.GetEnvironmentVariable("ProgramW6432"));
            RegisterImmutableDirectory(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            RegisterImmutableDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"));
            RegisterImmutableDirectory(GetVSInstallationDirectory());

            return;

            static string? GetVSInstallationDirectory()
            {
                string? dir = Environment.GetEnvironmentVariable("VSAPPIDDIR");

                // The path provided is not the installation root, but rather the location of devenv.exe.
                // __VSSPROPID.VSSPROPID_InstallDirectory has the same value.
                // Failing a better way to obtain the installation root, remove that suffix.
                // Obviously this is brittle against changes to the relative path of devenv.exe, however that seems
                // unlikely and should be easy to work around if ever needed.
                const string devEnvExeRelativePath = "Common7\\IDE\\";

                if (dir?.EndsWith(devEnvExeRelativePath, PathComparison) == true)
                {
                    dir = dir.Substring(0, dir.Length - devEnvExeRelativePath.Length);
                }

                return dir;
            }
        }

        /// <summary>
        ///     Shared singleton instance.
        /// </summary>
        public static FileClassifier Shared => s_sharedInstance.Value;

        /// <summary>
        ///     Try add paths found in the <c>NuGetPackageFolders</c> property value for a project into set of known immutable
        ///     paths.
        ///     Project files under any of these folders are considered non-modifiable.
        /// </summary>
        /// <remarks>
        ///     This value is used by <see cref="IsNonModifiable" />.
        ///     Files in the NuGet package cache are not expected to change over time, once they are created.
        /// </remarks>
        /// <remarks>
        ///     Example value: <c>"C:\Users\myusername\.nuget\;D:\LocalNuGetCache\"</c>
        /// </remarks>
        public void RegisterNuGetPackageFolders(string nuGetPackageFolders)
        {
            if (!string.IsNullOrEmpty(nuGetPackageFolders))
            {
                string[] folders = nuGetPackageFolders.Split(s_semicolonDelimiter, StringSplitOptions.RemoveEmptyEntries);
                foreach (string folder in folders)
                {
                    RegisterImmutableDirectory(folder);
                }
            }
        }

        private void RegisterImmutableDirectory(string? directory)
        {
            if (directory?.Length > 0)
            {
                string d = EnsureTrailingSlash(directory);
                _knownImmutableDirectory.TryAdd(d, d);
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
        public bool IsNonModifiable(string filePath) => _knownImmutableDirectory.Any(folder => filePath.StartsWith(folder.Key, PathComparison));
    }
}
