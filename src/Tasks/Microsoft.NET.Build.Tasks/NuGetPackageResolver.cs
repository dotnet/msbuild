// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    internal sealed class NuGetPackageResolver : IPackageResolver
    {
        private readonly FallbackPackagePathResolver _packagePathResolver;

        // Used when no package folders are provided, finds no packages.
        private static readonly NuGetPackageResolver s_noPackageFolderResolver = new NuGetPackageResolver();

        private NuGetPackageResolver()
        {
        }

        private NuGetPackageResolver(string userPackageFolder, IEnumerable<string> fallbackPackageFolders)
        {
            _packagePathResolver = new FallbackPackagePathResolver(userPackageFolder, fallbackPackageFolders);
        }

        public string GetPackageDirectory(string packageId, NuGetVersion version)
            => _packagePathResolver?.GetPackageDirectory(packageId, version);
        
        public string GetPackageDirectory(string packageId, NuGetVersion version, out string packageRoot)
        {
            var packageInfo = _packagePathResolver?.GetPackageInfo(packageId, version);
            if (packageInfo == null)
            {
                packageRoot = null;
                return null;
            }

            packageRoot = packageInfo.PathResolver.RootPath;
            return packageInfo.PathResolver.GetInstallPath(packageId, version);
        }

        public string ResolvePackageAssetPath(LockFileTargetLibrary package, string relativePath)
        {
            string packagePath = GetPackageDirectory(package.Name, package.Version);

            if (packagePath == null)
            {
                throw new BuildErrorException(
                    string.Format(Strings.PackageNotFound, package.Name, package.Version));
            }

            return Path.Combine(packagePath, NormalizeRelativePath(relativePath));
        }

        public static string NormalizeRelativePath(string relativePath)
            => relativePath.Replace('/', Path.DirectorySeparatorChar);

        public static NuGetPackageResolver CreateResolver(LockFile lockFile)
            => CreateResolver(lockFile.PackageFolders.Select(f => f.Path));

        public static NuGetPackageResolver CreateResolver(IEnumerable<string> packageFolders)
        {
            string userPackageFolder = packageFolders.FirstOrDefault();

            if (userPackageFolder == null)
            {
                return s_noPackageFolderResolver;
            }

            return new NuGetPackageResolver(userPackageFolder, packageFolders.Skip(1));
        }
    }
}
