// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    internal static class NuGetUtils
    {
        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), PackagingCoreConstants.EmptyFolder, StringComparison.Ordinal);
        }

        public static IEnumerable<LockFileItem> FilterPlaceHolderFiles(this IEnumerable<LockFileItem> files)
        {
            return files.Where(f => !IsPlaceholderFile(f.Path));
        }

        public static string GetLockFileLanguageName(string projectLanguage)
        {
            switch (projectLanguage)
            {
                case "C#": return "cs";
                case "F#": return "fs";
                default: return projectLanguage?.ToLowerInvariant();
            }
        }

        public static NuGetFramework ParseFrameworkName(string frameworkName)
        {
            return frameworkName == null ? null : NuGetFramework.Parse(frameworkName);
        }

        /// <summary>
        /// Gets PackageId from sourcePath.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static string GetPackageIdFromSourcePath(string sourcePath)
        {
            string packageId, unused;
            GetPackageParts(sourcePath, out packageId, out unused);
            return packageId;
        }

        /// <summary>
        /// Gets PackageId and package subpath from source path
        /// </summary>
        /// <param name="fullPath">full path to package file</param>
        /// <param name="packageId">package ID</param>
        /// <param name="packageSubPath">subpath of asset within package</param>
        public static void GetPackageParts(string fullPath, out string packageId, out string packageSubPath)
        {
            packageId = null;
            packageSubPath = null;
            try
            {
                // this method is just a temporary heuristic until we get metadata added to items created by the .NETCore SDK
                for (var dir = Directory.GetParent(fullPath); dir != null; dir = dir.Parent)
                {
                    var nuspecs = dir.GetFiles("*.nuspec");

                    if (nuspecs.Length > 0)
                    {
                        packageId = Path.GetFileNameWithoutExtension(nuspecs[0].Name);
                        packageSubPath = fullPath.Substring(dir.FullName.Length + 1).Replace('\\', '/');
                        break;
                    }
                }
            }
            catch (Exception)
            { }

            return;

        }
    }
}
