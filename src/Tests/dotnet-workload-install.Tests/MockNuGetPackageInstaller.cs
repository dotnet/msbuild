// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class MockNuGetPackageDownloader : INuGetPackageDownloader
    {
        private readonly string _downloadPath;
        private readonly bool _manifestDownload;

        public List<(PackageId, NuGetVersion, DirectoryPath?, PackageSourceLocation)> DownloadCallParams = new();

        public List<string> DownloadCallResult = new List<string>();

        public List<(string, DirectoryPath)> ExtractCallParams = new List<(string, DirectoryPath)>();

        public MockNuGetPackageDownloader(string dotnetRoot, bool manifestDownload = false)
        {
            _manifestDownload = manifestDownload;
            _downloadPath = Path.Combine(dotnetRoot, "metadata", "temp");
            Directory.CreateDirectory(_downloadPath);
        }

        public Task<string> DownloadPackageAsync(PackageId packageId,
            NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false,
            DirectoryPath? downloadFolder = null)
        {
            DownloadCallParams.Add((packageId, packageVersion, downloadFolder, packageSourceLocation));
            var path = Path.Combine(_downloadPath, "mock.nupkg");
            DownloadCallResult.Add(path);
            File.WriteAllText(path, string.Empty);
            return Task.FromResult(path);
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
        {
            ExtractCallParams.Add((packagePath, targetFolder));
            if (_manifestDownload)
            {
                Directory.CreateDirectory(Path.Combine(targetFolder.Value, "data"));
            }
            return Task.FromResult(new List<string>() as IEnumerable<string>);
        }

        public Task<NuGetVersion> GetLatestPackageVerion(PackageId packageId, PackageSourceLocation packageSourceLocation = null, bool includePreview = false)
        {
            return Task.FromResult(new NuGetVersion("10.0.0"));
        }

        public Task<string> GetPackageUrl(PackageId packageId,
            NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false)
        {
            return Task.FromResult("mock-url-" + packageId.ToString());
        }
    }
}
