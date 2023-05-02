// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
