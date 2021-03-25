// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.NuGetPackageInstaller
{
    internal class NuGetPackagePathResolver : PackagePathResolver
    {
        public NuGetPackagePathResolver(string rootDirectory) : base(rootDirectory, false)
        {
        }

        public override string GetPackageDirectoryName(PackageIdentity packageIdentity)
        {
            return string.Empty; // Extract package directly to target directory
        }

        public override string GetPackageFileName(PackageIdentity packageIdentity)
        {
            return packageIdentity.Id + PackagingCoreConstants.NupkgExtension;
        }
    }
}
