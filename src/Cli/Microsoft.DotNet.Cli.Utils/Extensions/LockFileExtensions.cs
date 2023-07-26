// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Common;
using NuGet.Packaging;
using NuGet.ProjectModel;

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
