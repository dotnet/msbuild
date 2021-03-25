// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageInstaller
{
    internal class NuGetPackageInstaller : INuGetPackageInstaller
    {
        private static readonly string sourceUrl = "https://pkgs.dev.azure.com/azure-public/vside/_packaging/xamarin-impl/nuget/v3/index.json";
        private readonly ILogger _logger;
        private readonly string _packageInstallDir;

        public NuGetPackageInstaller(string packageInstallDir, ILogger logger = null)
        {
            _packageInstallDir = packageInstallDir;
            _logger = logger ?? new NullLogger();
        }

        public async Task<string> InstallPackageAsync(PackageId packageId, NuGetVersion packageVersion)
        {
            var cancellationToken = CancellationToken.None;
            var cache = new SourceCacheContext() { DirectDownload = true, NoCache = true };
            var source = Repository.Factory.GetCoreV3(sourceUrl);
            var findPackageByIdResource = await source.GetResourceAsync<FindPackageByIdResource>();
            var nupkgPath = Path.Combine(_packageInstallDir, packageId.ToString(), packageVersion.ToNormalizedString(), $"{packageId}.{packageVersion.ToNormalizedString()}.nupkg");
            Directory.CreateDirectory(Path.GetDirectoryName(nupkgPath));
            using var destinationStream = File.Create(nupkgPath);
            var success = await findPackageByIdResource.CopyNupkgToStreamAsync(
                id: packageId.ToString(),
                version: packageVersion,
                destination: destinationStream,
                cacheContext: cache,
                logger: _logger,
                cancellationToken: cancellationToken);

            if (!success)
            {
                throw new Exception($"Downloading {packageId} version {packageVersion.ToNormalizedString()} failed.");
            }

            return nupkgPath;
        }

        public async Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, string targetFolder)
        {
            using var packageStream = File.OpenRead(packagePath);
            var packageReader = new PackageFolderReader(targetFolder);
            var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv3,
                        XmlDocFileSaveMode.None,
                        clientPolicyContext: null,
                        logger: _logger);
            var packagePathResolver = new NuGetPackagePathResolver(targetFolder);
            var cancellationToken = CancellationToken.None;

            return await PackageExtractor.ExtractPackageAsync(
                source: targetFolder,
                packageStream: packageStream,
                packagePathResolver: packagePathResolver,
                packageExtractionContext: packageExtractionContext,
                token: cancellationToken);
        }
    }
}
