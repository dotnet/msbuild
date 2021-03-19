// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class NuGetPackageInstaller
    {
        private static readonly string sourceUrl = "https://api.nuget.org/v3/index.json";
        private readonly ILogger _logger;
        private readonly string _packageInstallDir;

        public NuGetPackageInstaller(string packageInstallDir, ILogger logger = null)
        {
            _packageInstallDir = packageInstallDir;
            _logger = logger ?? new NullLogger();
        }

        public async Task<string> InstallPackageAsync(string packageId, NuGetVersion packageVersion)
        {
            var cancellationToken = CancellationToken.None;
            var cache = new SourceCacheContext() { DirectDownload = true, NoCache = true };
            var source = Repository.Factory.GetCoreV3(sourceUrl);
            var findPackageByIdResource = await source.GetResourceAsync<FindPackageByIdResource>();
            var nupkgPath = Path.Combine(_packageInstallDir, packageId, packageVersion.ToNormalizedString(), $"{packageId}.{packageVersion.ToNormalizedString()}.nupkg");
            Directory.CreateDirectory(Path.GetDirectoryName(nupkgPath));
            using (FileStream destinationStream = File.Create(nupkgPath))
            {
                var success = await findPackageByIdResource.CopyNupkgToStreamAsync(
                    id: packageId,
                    version: packageVersion,
                    destination: destinationStream,
                    cacheContext: cache,
                    logger: _logger,
                    cancellationToken: cancellationToken);

                if (!success)
                {
                    throw new Exception($"Downloading {packageId} version {packageVersion.ToNormalizedString()} failed.");
                }
            }

            return nupkgPath;
        }
    }
}
