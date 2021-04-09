// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    // Extract NuGet package content directly to the specified target directory (instead of creating subdirs)
    internal class NuGetPackagePathResolver : PackagePathResolver
    {
        public NuGetPackagePathResolver(string rootDirectory) : base(rootDirectory, false)
        {
        }

        public override string GetPackageDirectoryName(PackageIdentity packageIdentity)
        {
            return string.Empty;
        }

        public override string GetPackageFileName(PackageIdentity packageIdentity)
        {
            return packageIdentity.Id + PackagingCoreConstants.NupkgExtension;
        }
    }
}
