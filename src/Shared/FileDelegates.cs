// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// delegate for System.IO.Directory.GetFiles, used for testing
    /// </summary>
    /// <param name="path">Directory path to start search for files in</param>
    /// <param name="searchPattern">pattern of files to match</param>
    /// <returns>string array of files which match search pattern</returns>
    internal delegate string[] DirectoryGetFiles(string path, string searchPattern);

    /// <summary>
    /// delegate for Directory.GetDirectories.
    /// </summary>
    /// <param name="path">The path to get directories for.</param>
    /// <param name="pattern">The pattern to search for.</param>
    /// <returns>An array of directories.</returns>
    internal delegate string[] GetDirectories(string path, string pattern);

    /// <summary>
    /// Delegate for System.IO.Directory.Exists
    /// </summary>
    /// <param name="path">Directory path to check if it exists</param>
    /// <returns>true if directory exists</returns>
    internal delegate bool DirectoryExists(string path);

    /// <summary>
    /// File exists delegate
    /// </summary>
    /// <param name="path">The path to check for existence.</param>
    /// <returns>'true' if the file exists.</returns>
    internal delegate bool FileExists(string path);

    /// <summary>
    /// File.Copy delegate
    /// </summary>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    internal delegate void FileCopy(string source, string destination);

    /// <summary>
    /// File.Delete delegate
    /// </summary>
    /// <param name="path"></param>
    internal delegate void FileDelete(string path);

    /// <summary>
    /// File create delegate
    /// </summary>
    /// <param name="path">The path to create.</param>
    internal delegate FileStream FileCreate(string path);
}