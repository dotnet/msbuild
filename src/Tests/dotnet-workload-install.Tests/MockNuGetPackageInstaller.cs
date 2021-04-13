// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class MockNuGetPackageDownloader : INuGetPackageDownloader
    {
        private readonly string _downloadPath;

        public List<(PackageId, NuGetVersion)> DownloadCallParams = new List<(PackageId, NuGetVersion)>();

        public List<string> DownloadCallResult = new List<string>();

        public List<(string, string)> ExtractCallParams = new List<(string, string)>();

        public MockNuGetPackageDownloader(string dotnetRoot)
        {
            _downloadPath = Path.Combine(dotnetRoot, "metadata", "temp");
            Directory.CreateDirectory(_downloadPath);
        }

        public Task<string> DownloadPackageAsync(PackageId packageId,
            NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false)
        {
            DownloadCallParams.Add((packageId, packageVersion));
            var path = Path.Combine(_downloadPath, "mock.nupkg");
            DownloadCallResult.Add(path);
            File.WriteAllText(path, string.Empty);
            return Task.FromResult(path);
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, string targetFolder)
        {
            ExtractCallParams.Add((packagePath, targetFolder));
            return Task.FromResult(new List<string>() as IEnumerable<string>);
        }
    }
}
