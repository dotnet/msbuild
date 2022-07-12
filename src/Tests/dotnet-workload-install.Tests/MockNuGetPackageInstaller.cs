// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
        private NuGetVersion _lastPackageVersion = new NuGetVersion("1.0.0");

        public List<(PackageId id, NuGetVersion version, DirectoryPath? downloadFolder, PackageSourceLocation packageSourceLocation)> DownloadCallParams = new();

        public List<string> DownloadCallResult = new List<string>();

        public List<(string, DirectoryPath)> ExtractCallParams = new List<(string, DirectoryPath)>();

        public HashSet<string> PackageIdsToNotFind { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public MockNuGetPackageDownloader(string dotnetRoot = null, bool manifestDownload = false)
        {
            _manifestDownload = manifestDownload;
            _downloadPath = dotnetRoot == null ? string.Empty : Path.Combine(dotnetRoot, "metadata", "temp");
            if (_downloadPath != string.Empty)
            {
                Directory.CreateDirectory(_downloadPath);
            }
        }

        public Task<string> DownloadPackageAsync(PackageId packageId,
            NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false,
            DirectoryPath? downloadFolder = null)
        {
            DownloadCallParams.Add((packageId, packageVersion, downloadFolder, packageSourceLocation));

            if (PackageIdsToNotFind.Contains(packageId.ToString()))
            {
                return Task.FromException<string>(new NuGetPackageNotFoundException("Package not found: " + packageId.ToString()));
            }

            var path = Path.Combine(_downloadPath, "mock.nupkg");
            DownloadCallResult.Add(path);
            if (_downloadPath != string.Empty)
            {
                File.WriteAllText(path, string.Empty);
            }
            _lastPackageVersion = packageVersion ?? new NuGetVersion("1.0.42");
            return Task.FromResult(path);
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
        {
            ExtractCallParams.Add((packagePath, targetFolder));
            if (_manifestDownload)
            {
                var dataFolder = Path.Combine(targetFolder.Value, "data");
                Directory.CreateDirectory(dataFolder);
                string manifestContents = $@"{{
  ""version"": ""{_lastPackageVersion.ToString()}"",
  ""workloads"": {{
    }}
  }},
  ""packs"": {{
  }}
}}";
                   
               File.WriteAllText(Path.Combine(dataFolder, "WorkloadManifest.json"), manifestContents);
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
            return Task.FromResult($"http://mock-url/{packageId}.{packageVersion}.nupkg");
        }
    }
}
