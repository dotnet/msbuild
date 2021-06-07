// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class PackageManager : IDisposable
    {
        private const string NuGetOrgFeed = "https://api.nuget.org/v3/index.json";
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private string _packageLocation = TestUtils.CreateTemporaryFolder("packages");
        private ConcurrentDictionary<string, string> _installedPackages = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string PackTestTemplatesNuGetPackage()
        {
            string dir = Path.GetDirectoryName(typeof(PackageManager).GetTypeInfo().Assembly.Location) ?? string.Empty;
            string projectToPack = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "Microsoft.TemplateEngine.TestTemplates.csproj");
            return PackNuGetPackage(projectToPack);
        }

        public async Task<string> GetNuGetPackage(string templatePackName, NuGetVersion? minimumVersion = null, ILogger? logger = null)
        {
            logger = logger ?? NullLogger.Instance;
            NuGetHelper nuGetHelper = new NuGetHelper(_packageLocation, logger);
            try
            {
                logger.LogDebug($"[NuGet Package Manager] Trying to get semaphore.");
                await semaphore.WaitAsync().ConfigureAwait(false);
                logger.LogDebug($"[NuGet Package Manager] Semaphore acquired.");
                if (_installedPackages.TryGetValue(templatePackName, out string? packagePath))
                {
                    return packagePath;
                }

                for (int retry = 0; retry < 5; retry++)
                {
                    try
                    {
                        logger.LogDebug($"[NuGet Package Manager][attempt: {retry + 1}] Downloading package {templatePackName}, minimum version: {minimumVersion?.ToNormalizedString()}");
                        string downloadedPackage = await nuGetHelper.DownloadPackageAsync(
                            templatePackName,
                            additionalSources: new [] { NuGetOrgFeed },
                            minimumVersion: minimumVersion).ConfigureAwait(false);
                        _installedPackages[templatePackName] = downloadedPackage;
                        logger.LogDebug($"[NuGet Package Manager][attempt: {retry + 1}] The package {templatePackName} was successfully downloaded.");
                        return downloadedPackage;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"[NuGet Package Manager] Download failed: package {templatePackName}, details: {ex}");
                        //retry failed download
                    }
                    logger.LogDebug($"[NuGet Package Manager][attempt: {retry + 1}] Will wait for 1 sec before re-attempt.");
                    await Task.Delay(1000).ConfigureAwait(false);
                    logger.LogDebug($"[NuGet Package Manager][attempt: {retry + 1}] Waiting over.");
                }
                logger.LogError($"[NuGet Package Manager] Failed to download {templatePackName} after 5 retries.");
                throw new Exception($"Failed to download {templatePackName} after 5 retries");
            }
            finally
            {
                logger.LogDebug($"[NuGet Package Manager] Releasing semaphore.");
                semaphore.Release();
                logger.LogDebug($"[NuGet Package Manager] Semaphore released.");
            }
        }

        public string PackNuGetPackage(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException("projectPath cannot be null", nameof(projectPath));
            }
            string absolutePath = Path.GetFullPath(projectPath);
            if (!File.Exists(projectPath))
            {
                throw new ArgumentException($"{projectPath} doesn't exist", nameof(projectPath));
            }
            lock (string.Intern(absolutePath.ToLowerInvariant()))
            {
                if (_installedPackages.TryGetValue(absolutePath, out string? packagePath))
                {
                    return packagePath;
                }

                var info = new ProcessStartInfo("dotnet", $"pack {absolutePath} -o {_packageLocation}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                Process p = Process.Start(info) ?? throw new Exception("Failed to start dotnet process.");
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    throw new Exception($"Failed to pack the project {projectPath}");
                }

                string createdPackagePath = Directory.GetFiles(_packageLocation).Aggregate(
                    (latest, current) => (latest == null) ? current : File.GetCreationTimeUtc(current) > File.GetCreationTimeUtc(latest) ? current : latest);
                _installedPackages[absolutePath] = createdPackagePath;
                return createdPackagePath;
            }
        }

        public void Dispose() => Directory.Delete(_packageLocation, true);

        private class NuGetHelper
        {
            private readonly string _packageLocation;
            private readonly ILogger _nugetLogger;
            private readonly SourceCacheContext _cacheSettings = new SourceCacheContext()
            {
                NoCache = true,
                DirectDownload = true
            };

            internal NuGetHelper (string packageLocation, ILogger? logger = null)
            {
                _packageLocation = packageLocation;
                _nugetLogger = logger ?? NullLogger.Instance;
            }

            internal async Task<string> DownloadPackageAsync(
                string identifier,
                string? version = null,
                IEnumerable<string>? additionalSources = null,
                NuGetVersion? minimumVersion = null,
                CancellationToken cancellationToken = default)
            {
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    throw new ArgumentException($"{nameof(identifier)} cannot be null or empty", nameof(identifier));
                }

                IEnumerable<PackageSource> packagesSources = LoadNuGetSources(additionalSources?.ToArray() ?? Array.Empty<string>());

                NuGetVersion packageVersion;
                PackageSource source;
                IPackageSearchMetadata packageMetadata;

                if (string.IsNullOrWhiteSpace(version))
                {
                    _nugetLogger.LogDebug($"[NuGet Package Manager] Getting latest version for the package {identifier}.");
                    (source, packageMetadata) = await GetLatestVersionInternalAsync(identifier, packagesSources, cancellationToken).ConfigureAwait(false);
                    if (minimumVersion != null && packageMetadata.Identity.Version < minimumVersion)
                    {
                        _nugetLogger.LogError($"[NuGet Package Manager] Failed to find the package with version {minimumVersion} or later.");
                        throw new Exception($"Failed to find the package with version {minimumVersion} or later");
                    }
                }
                else
                {
                    _nugetLogger.LogDebug($"[NuGet Package Manager] Getting package metadata {identifier}::{version}.");
                    packageVersion = new NuGetVersion(version);
                    (source, packageMetadata) = await GetPackageMetadataAsync(identifier, packageVersion, packagesSources, cancellationToken).ConfigureAwait(false);
                }

                _nugetLogger.LogDebug($"[NuGet Package Manager] Getting repository for source {source.Source}.");
                FindPackageByIdResource resource;
                SourceRepository repository = Repository.Factory.GetCoreV3(source);
                try
                {
                    resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _nugetLogger.LogError($"[NuGet Package Manager] Failed to load NuGet source {source.Source}, details: {e}.");
                    throw new Exception($"Failed to load NuGet source {source.Source}", e);
                }
                _nugetLogger.LogDebug($"[NuGet Package Manager] Repository for source {source.Source} is loaded.");

                string filePath = Path.Combine(_packageLocation, packageMetadata.Identity.Id + "." + packageMetadata.Identity.Version + ".nupkg");
                if (File.Exists(filePath))
                {
                    _nugetLogger.LogError($"[NuGet Package Manager] {filePath} already exists.");
                    throw new Exception($"{filePath} already exists");
                }
                try
                {
                    _nugetLogger.LogDebug($"[NuGet Package Manager] Started download to {filePath}.");
                    using Stream packageStream = File.Create(filePath);
                    if (await resource.CopyNupkgToStreamAsync(
                        packageMetadata.Identity.Id,
                        packageMetadata.Identity.Version,
                        packageStream,
                        _cacheSettings,
                        _nugetLogger,
                        cancellationToken).ConfigureAwait(false))
                    {
                        _nugetLogger.LogDebug($"[NuGet Package Manager] Download finished successfully.");
                        return filePath;
                    }
                    else
                    {
                        _nugetLogger.LogError($"[NuGet Package Manager] Failed to download {packageMetadata.Identity.Id}, version: {packageMetadata.Identity.Version} from {source.Source}");
                        throw new Exception($"Failed to download {packageMetadata.Identity.Id}, version: {packageMetadata.Identity.Version} from {source.Source}");
                    }
                }
                catch (Exception e)
                {
                    _nugetLogger.LogError($"[NuGet Package Manager] Failed to download {packageMetadata.Identity.Id}, version: {packageMetadata.Identity.Version} from {source.Source}, details: {e}.");
                    throw new Exception($"Failed to download {packageMetadata.Identity.Id}, version: {packageMetadata.Identity.Version} from {source.Source}", e);
                }
            }

            private async Task<(PackageSource, IPackageSearchMetadata)> GetLatestVersionInternalAsync(
                string packageIdentifier,
                IEnumerable<PackageSource> packageSources,
                CancellationToken cancellationToken = default)
            {
                if (string.IsNullOrWhiteSpace(packageIdentifier))
                {
                    throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
                }
                _ = packageSources ?? throw new ArgumentNullException(nameof(packageSources));

                _nugetLogger.LogDebug($"[NuGet Package Manager] Checking package metadata in all sources, package: {packageIdentifier}.");

                (PackageSource Source, IEnumerable<IPackageSearchMetadata>? FoundPackages)[] foundPackagesBySource =
                    await Task.WhenAll(
                        packageSources.Select(source => GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, cancellationToken)))
                              .ConfigureAwait(false);

                if (!foundPackagesBySource.Where(result => result.FoundPackages != null).Any())
                {
                    _nugetLogger.LogError($"[NuGet Package Manager] Failed to load NuGet sources {string.Join(";", packageSources.Select(source => source.Source))}.");
                    throw new Exception($"Failed to load NuGet sources {string.Join(";", packageSources.Select(source => source.Source))}");
                }

                var accumulativeSearchResults = foundPackagesBySource
                    .Where(r => r.FoundPackages != null)
                    .SelectMany(result => result.FoundPackages!.Select(package => (result.Source, package)));

                _nugetLogger.LogDebug($"[NuGet Package Manager] Found {accumulativeSearchResults.Count()} matches.");

                if (!accumulativeSearchResults.Any())
                {
                    _nugetLogger.LogError($"[NuGet Package Manager] {packageIdentifier} is not found in {string.Join(";", packageSources.Select(source => source.Source))}.");
                    throw new Exception($"{packageIdentifier} is not found in {string.Join(";", packageSources.Select(source => source.Source))}");
                }

                (PackageSource, IPackageSearchMetadata) latestVersion = accumulativeSearchResults.Aggregate(
                    (max, current) =>
                    {
                        if (max == default)
                        {
                            return current;
                        }

                        return current.package.Identity.Version > max.package.Identity.Version ? current : max;
                    });
                _nugetLogger.LogDebug($"[NuGet Package Manager] Latest version is {latestVersion.Item2.Identity.Id}::{latestVersion.Item2.Identity.Version}, source: {latestVersion.Item1.Source}.");
                return latestVersion;
            }

            private async Task<(PackageSource, IPackageSearchMetadata)> GetPackageMetadataAsync(
                string packageIdentifier,
                NuGetVersion packageVersion,
                IEnumerable<PackageSource> sources,
                CancellationToken cancellationToken = default)
            {
                if (string.IsNullOrWhiteSpace(packageIdentifier))
                {
                    throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
                }
                _ = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
                _ = sources ?? throw new ArgumentNullException(nameof(sources));

                bool atLeastOneSourceValid = false;
                _nugetLogger.LogDebug($"[NuGet Package Manager] Loading package {packageIdentifier} metadata from all sources.");
                using CancellationTokenSource linkedCts =
                          CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var tasks = sources.Select(source => GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, linkedCts.Token)).ToList();
                while (tasks.Any())
                {
                    var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                    tasks.Remove(finishedTask);
                    (PackageSource Source, IEnumerable<IPackageSearchMetadata> FoundPackages) result = await finishedTask.ConfigureAwait(false);
                    _nugetLogger.LogDebug($"[NuGet Package Manager] Processed source {result.Source.Source}, found {result.FoundPackages.Count()} packages.");
                    if (result.FoundPackages == null)
                    {
                        continue;
                    }
                    atLeastOneSourceValid = true;
                    IPackageSearchMetadata? matchedVersion = result.FoundPackages!.FirstOrDefault(package => package.Identity.Version == packageVersion);
                    if (matchedVersion != null)
                    {
                        _nugetLogger.LogDebug($"[NuGet Package Manager] Processed source {result.Source.Source}, found {matchedVersion.Identity.Id}:: {matchedVersion.Identity.Version} package, cancelling other tasks.");
                        linkedCts.Cancel();
                        return (result.Source, matchedVersion);
                    }
                    _nugetLogger.LogDebug($"[NuGet Package Manager] Processed source {result.Source.Source}, no package with version {packageVersion} found.");
                }
                if (!atLeastOneSourceValid)
                {
                    _nugetLogger.LogError($"[NuGet Package Manager] Failed to load NuGet sources {string.Join(";", sources.Select(source => source.Source))}.");
                    throw new Exception($"Failed to load NuGet sources {string.Join(";", sources.Select(source => source.Source))}");
                }

                _nugetLogger.LogError($"[NuGet Package Manager] {packageIdentifier}, version: {packageVersion} is not found in {string.Join(";", sources.Select(source => source.Source))}.");
                throw new Exception($"{packageIdentifier}, version: {packageVersion} is not found in {string.Join(";", sources.Select(source => source.Source))}");
            }

            private async Task<(PackageSource Source, IEnumerable<IPackageSearchMetadata>? FoundPackages)> GetPackageMetadataAsync(
                PackageSource source,
                string packageIdentifier,
                bool includePrerelease = false,
                CancellationToken cancellationToken = default)
            {
                if (string.IsNullOrWhiteSpace(packageIdentifier))
                {
                    throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
                }
                _ = source ?? throw new ArgumentNullException(nameof(source));

                try
                {
                    _nugetLogger.LogDebug($"[NuGet Package Manager] Getting metadata for package {packageIdentifier} from source {source.Source}.");
                    SourceRepository repository = Repository.Factory.GetCoreV3(source);
                    PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken).ConfigureAwait(false);
                    IEnumerable<IPackageSearchMetadata> foundPackages = await resource.GetMetadataAsync(
                        packageIdentifier,
                        includePrerelease: includePrerelease,
                        includeUnlisted: false,
                        _cacheSettings,
                        _nugetLogger,
                        cancellationToken).ConfigureAwait(false);
                    _nugetLogger.LogDebug($"[NuGet Package Manager] Found {foundPackages.Count()} packages in source {source.Source}.");
                    return (source, foundPackages);
                }
                catch (Exception ex)
                {
                    //ignore errors
                    _nugetLogger.LogWarning($"Retrieving info from {source.Source} failed, details: {ex}.");
                    return (source, null);
                }
            }

            private IEnumerable<PackageSource> LoadNuGetSources(params string[] additionalSources)
            {
                IEnumerable<PackageSource> defaultSources;
                string currentDirectory = string.Empty;
                try
                {
                    _nugetLogger.LogDebug($"[NuGet Package Manager] loading default sources");
                    currentDirectory = Directory.GetCurrentDirectory();
                    ISettings settings = global::NuGet.Configuration.Settings.LoadDefaultSettings(currentDirectory);
                    PackageSourceProvider packageSourceProvider = new PackageSourceProvider(settings);
                    defaultSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
                    foreach (var source in defaultSources)
                    {
                        _nugetLogger.LogDebug($"[NuGet Package Manager] Loaded source: {source.Source}");
                    }
                }
                catch (Exception ex)
                {
                    _nugetLogger.LogError($"[NuGet Package Manager] Failed to load NuGet sources configured for the folder {currentDirectory}, details: {ex}.");
                    throw new Exception($"Failed to load NuGet sources configured for the folder {currentDirectory}", ex);
                }

                if (!additionalSources.Any())
                {
                    if (!defaultSources.Any())
                    {
                        _nugetLogger.LogError($"[NuGet Package Manager] No NuGet sources are defined or enabled.");
                        throw new Exception("No NuGet sources are defined or enabled");
                    }
                    return defaultSources;
                }

                List<PackageSource> customSources = new List<PackageSource>();
                _nugetLogger.LogDebug($"[NuGet Package Manager] loading additional sources");
                foreach (string source in additionalSources)
                {
                    _nugetLogger.LogDebug($"[NuGet Package Manager] loading source {source}.");
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        continue;
                    }
                    if (defaultSources.Any(s => s.Source.Equals(source, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    PackageSource packageSource = new PackageSource(source);
                    if (packageSource.TrySourceAsUri == null)
                    {
                        _nugetLogger.LogDebug($"[NuGet Package Manager] {source} is not a valid source.");
                        continue;
                    }
                    customSources.Add(packageSource);
                    _nugetLogger.LogDebug($"[NuGet Package Manager] Loaded source: {packageSource.Source}");
                }

                IEnumerable<PackageSource> retrievedSources = customSources.Concat(defaultSources);
                if (!retrievedSources.Any())
                {
                    throw new Exception("No NuGet sources are defined or enabled");
                }
                return retrievedSources;
            }
        }
    }
}
