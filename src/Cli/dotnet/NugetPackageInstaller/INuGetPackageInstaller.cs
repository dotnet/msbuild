// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal interface INuGetPackageDownloader
    {
        Task<string> DownloadPackageAsync(PackageId packageId, NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false);

        Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, string targetFolder);
    }
}
