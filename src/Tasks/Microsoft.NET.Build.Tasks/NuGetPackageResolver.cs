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
            return _packagePathResolver.GetPackageDirectory(packageId, version);
        }

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
