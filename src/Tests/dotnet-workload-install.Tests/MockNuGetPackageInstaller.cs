// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class MockNuGetPackageDownloader : INuGetPackageDownloader
    {
        private readonly string _installPath;

        public List<(PackageId, NuGetVersion)> InstallCallParams = new List<(PackageId, NuGetVersion)>();

        public List<string> InstallCallResult = new List<string>();

        public List<(string, string)> ExtractCallParams = new List<(string, string)>();

        public MockNuGetPackageDownloader(string dotnetRoot)
        {
            _installPath = Path.Combine(dotnetRoot, "metadata", "temp");
            Directory.CreateDirectory(_installPath);
        }

        public Task<string> DownloadPackageAsync(PackageId packageId, NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false)
        {
            InstallCallParams.Add((packageId, packageVersion));
            var path = Path.Combine(_installPath, "mock.nupkg");
            InstallCallResult.Add(path);
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
