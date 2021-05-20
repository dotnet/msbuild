// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IWorkloadManifestUpdater
    {
        Task UpdateAdvertisingManifestsAsync(bool includePreviews, DirectoryPath? offlineCache = null);

        IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> CalculateManifestUpdates();

        Task<IEnumerable<string>> DownloadManifestPackagesAsync(bool includePreviews, DirectoryPath downloadPath);

        Task ExtractManifestPackagesToTempDirAsync(IEnumerable<string> manifestPackages, DirectoryPath tempDir);

        IEnumerable<string> GetManifestPackageUrls(bool includePreviews);
    }
}
