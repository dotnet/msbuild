// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    static partial class NuGetUtilities
    {
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
