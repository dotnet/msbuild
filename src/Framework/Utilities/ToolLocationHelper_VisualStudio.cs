// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    /// <summary>
    /// Cache the VS Install folders for particular range of VS versions
    /// </summary>
    private static Dictionary<string, string[]> s_vsInstallFolders;

    /// <summary>
    ///  Returns folders in VS installs of specifid range of versions, starting with 15.0.
    /// </summary>
    /// <param name="minVersion">Optional. If specified, only VS instances with this or bigger version will be included.</param>
    /// <param name="maxVersion">Optional. If specified, only VS instances with smaller versions will be included. For instance, 16.0 means that only 15.* versions will be included.</param>
    /// <param name="subFolder">Optional. If specified, the returned list will contain [VSInstallDir][subFolder] paths</param>
    /// <returns>Returns folders in VS installs in VS version descending order</returns>
    public static IEnumerable<string> GetFoldersInVSInstalls(Version minVersion = null, Version maxVersion = null, string subFolder = null)
    {
        string versionRange = $"{minVersion?.ToString() ?? string.Empty};{maxVersion?.ToString() ?? string.Empty}";
        string[] vsInstallFolders;

        lock (s_locker)
        {
            s_vsInstallFolders ??= new Dictionary<string, string[]>();

            if (!s_vsInstallFolders.TryGetValue(versionRange, out vsInstallFolders))
            {
                IList<VisualStudioInstance> vsInstances = VisualStudioLocationHelper.GetInstances();

                var vsInstancePaths = vsInstances
                    .Where(i => (minVersion == null || i.Version >= minVersion) && (maxVersion == null || i.Version < maxVersion))
                    .OrderByDescending(i => i.Version)
                    .Select(i => i.Path);

                vsInstallFolders = vsInstancePaths.ToArray();
                s_vsInstallFolders[versionRange] = vsInstallFolders;
            }
        }

        var folders = string.IsNullOrEmpty(subFolder) ? vsInstallFolders : vsInstallFolders.Select(i => Path.Combine(i, subFolder));

        return folders;
    }

    /// <summary>
    ///  Returns folders in VS installs of specified range of versions (starting with 15.0) separated by ';'.
    /// </summary>
    /// <param name="minVersionString">Optional. If specified, only VS instances with this or bigger version will be included.</param>
    /// <param name="maxVersionString">Optional. If specified, only VS instances with smaller versions will be included. For instance, 16.0 means that only 15.* versions will be included.</param>
    /// <param name="subFolder">Optional. If specified, the returned list will contain [VSInstallDir][subFolder] paths</param>
    /// <returns>Returns folders in VS installs in VS version descending order separated by ';'</returns>
    public static string GetFoldersInVSInstallsAsString(string minVersionString = null, string maxVersionString = null, string subFolder = null)
    {
        string foldersString = string.Empty;

        try
        {
            Version minVersion, maxVersion;

            if (string.IsNullOrEmpty(minVersionString) || !Version.TryParse(minVersionString, out minVersion))
            {
                minVersion = null;
            }
            if (string.IsNullOrEmpty(maxVersionString) || !Version.TryParse(maxVersionString, out maxVersion))
            {
                maxVersion = null;
            }

            var folders = GetFoldersInVSInstalls(minVersion, maxVersion, subFolder);

            if (folders.Any())
            {
                foldersString = string.Join(";", folders);
            }
        }
        catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
        {
            // This method will be used in vc props and we don't want to fail project load if it throws for some non critical reason.
        }

        return foldersString;
    }

    /// <summary>
    /// Finds first folder in the list which contains all given files. Returns an empty string if not found.
    /// </summary>
    /// <param name="possibleRoots">Root folders separated by ';'</param>
    /// <param name="relativeFilePaths">Relative file paths to find under root folders, separated by ';'.</param>
    /// <returns>The first root folder in the given list, which contains all files. Empty string if not found.</returns>
    public static string FindRootFolderWhereAllFilesExist(string possibleRoots, string relativeFilePaths)
    {
        if (!string.IsNullOrEmpty(possibleRoots))
        {
            var roots = possibleRoots.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries);
            var files = relativeFilePaths.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries);

            bool allFilesFound;
            foreach (var root in roots)
            {
                allFilesFound = true;
                foreach (var file in files)
                {
                    try
                    {
                        string fullPath = Path.Combine(root, file);

                        if (!FileSystems.Default.FileExists(fullPath))
                        {
                            allFilesFound = false;
                            break;
                        }
                    }
                    catch (ArgumentException)
                    {
                        allFilesFound = false;
                        break;
                    }
                }

                if (allFilesFound)
                {
                    return root;
                }
            }
        }

        return string.Empty;
    }
}
