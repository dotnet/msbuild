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
    internal class NuGetPackageResolver : IPackageResolver
    {
        private readonly FallbackPackagePathResolver _packagePathResolver;

        public NuGetPackageResolver(INuGetPathContext pathContext)
        {
            _packagePathResolver = new FallbackPackagePathResolver(pathContext);
        }

        public NuGetPackageResolver(string userPackageFolder, IEnumerable<string> fallbackPackageFolders)
        {
            _packagePathResolver = new FallbackPackagePathResolver(userPackageFolder, fallbackPackageFolders);
        }
        public string GetPackageDirectory(string packageId, NuGetVersion version)
        {
            string  packageRoot = null;
            return GetPackageDirectory(packageId, version, out packageRoot);
        }
        public string GetPackageDirectory(string packageId, NuGetVersion version, out string packageRoot)
        {
            packageRoot = null;
            var pkginfo = _packagePathResolver.GetPackageInfo(packageId,version);
            if (pkginfo != null)
            {
                packageRoot = pkginfo.PathResolver.GetVersionListPath("");  //TODO Remove Once Nuget is updated to use FallbackPackagePathInfo.PathResolver.RootPath
            }
            return _packagePathResolver.GetPackageDirectory(packageId, version);
        }

        public string ResolvePackageAssetPath(LockFileTargetLibrary package, string relativePath)
        {
            string packagePath = GetPackageDirectory(package.Name, package.Version);
            return Path.Combine(packagePath, NormalizeRelativePath(relativePath));
        }

        public static string NormalizeRelativePath(string relativePath)
                => relativePath.Replace('/', Path.DirectorySeparatorChar);

        public static NuGetPackageResolver CreateResolver(LockFile lockFile, string projectPath)
        {
            NuGetPackageResolver packageResolver;

            string userPackageFolder = lockFile.PackageFolders.FirstOrDefault()?.Path;
            if (userPackageFolder != null)
            {
                var fallBackFolders = lockFile.PackageFolders.Skip(1).Select(f => f.Path);
                packageResolver = new NuGetPackageResolver(userPackageFolder, fallBackFolders);
            }
            else
            {
                NuGetPathContext nugetPathContext = NuGetPathContext.Create(Path.GetDirectoryName(projectPath));
                packageResolver = new NuGetPackageResolver(nugetPathContext);
            }

            return packageResolver;
        }
    }
}
