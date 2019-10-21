// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Common;
using NuGet.Packaging;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Cli.Utils
{
    static class LockFileExtensions
    {
        public static string GetPackageDirectory(this LockFile lockFile, LockFileTargetLibrary library)
        {
            var packageFolders = lockFile.GetNormalizedPackageFolders();

            var packageFoldersCount = packageFolders.Count();
            var userPackageFolder = packageFoldersCount == 1 ? string.Empty : packageFolders.First();
            var fallbackPackageFolders = packageFoldersCount > 1 ? packageFolders.Skip(1) : packageFolders;

            var packageDirectory = new FallbackPackagePathResolver(userPackageFolder, fallbackPackageFolders)
                .GetPackageDirectory(library.Name, library.Version);

            return packageDirectory;
        }

        public static IEnumerable<string> GetNormalizedPackageFolders(this LockFile lockFile)
        {
            return lockFile.PackageFolders.Select(p =>
                PathUtility.EnsureNoTrailingDirectorySeparator(p.Path));
        }
    }
}
