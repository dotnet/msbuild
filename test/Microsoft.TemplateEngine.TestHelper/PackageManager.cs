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
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class PackageManager : IDisposable
    {
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly ILogger _nugetLogger = NullLogger.Instance;

        private readonly SourceCacheContext _cacheSettings = new SourceCacheContext()
        {
            NoCache = true,
            DirectDownload = true
        };

        private string _packageLocation = TestUtils.CreateTemporaryFolder("packages");
        private ConcurrentDictionary<string, string> _installedPackages = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string PackTestTemplatesNuGetPackage()
        {
            string dir = Path.GetDirectoryName(typeof(PackageManager).GetTypeInfo().Assembly.Location) ?? string.Empty;
            string projectToPack = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "Microsoft.TemplateEngine.TestTemplates.csproj");
            return PackNuGetPackage(projectToPack);
        }

        public async Task<string> GetNuGetPackage(string templatePackName, ITestOutputHelper? log = null)
        {
            try
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                if (_installedPackages.TryGetValue(templatePackName, out string? packagePath))
                {
                    return packagePath;
                }

                for (int retry = 0; retry < 5; retry++)
                {
                    try
                    {
                        string downloadedPackage = await DownloadPackageAsync(templatePackName, log: log).ConfigureAwait(false);
                        _installedPackages[templatePackName] = downloadedPackage;
                        return downloadedPackage;
                    }
                    catch (Exception ex)
                    {
                        log?.WriteLine($"[NuGet Package Manager] Download failed: package {templatePackName}, details: {ex}");
                        //retry failed download
                    }
                    await Task.Delay(1000).ConfigureAwait(false);
                }
                throw new Exception($"Failed to download {templatePackName} after 5 retries");
            }
            finally
            {
                semaphore.Release();
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

        private async Task<string> DownloadPackageAsync(
            string identifier,
            string? version = null,
            IEnumerable<string>? additionalSources = null,
            ITestOutputHelper? log = null,
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
                (source, packageMetadata) = await GetLatestVersionInternalAsync(identifier, packagesSources, log, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                packageVersion = new NuGetVersion(version);
                (source, packageMetadata) = await GetPackageMetadataAsync(identifier, packageVersion, packagesSources, log, cancellationToken).ConfigureAwait(false);
            }

            FindPackageByIdResource resource;
            SourceRepository repository = Repository.Factory.GetCoreV3(source);
            try
            {
                resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to load NuGet source {source.Source}", e);
            }

            string filePath = Path.Combine(_packageLocation, packageMetadata.Identity.Id + "." + packageMetadata.Identity.Version + ".nupkg");
            if (File.Exists(filePath))
            {
                throw new Exception($"{filePath} already exists");
            }
            try
            {
                using Stream packageStream = File.Create(filePath);
                if (await resource.CopyNupkgToStreamAsync(
                    packageMetadata.Identity.Id,
                    packageMetadata.Identity.Version,
                    packageStream,
                    _cacheSettings,
                    _nugetLogger,
                    cancellationToken).ConfigureAwait(false))
                {
                    return filePath;
                }
                else
                {
                    throw new Exception($"Failed to download {packageMetadata.Identity.Id}, version: {packageMetadata.Identity.Version} from {source.Source}");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to download {packageMetadata.Identity.Id}, version: {packageMetadata.Identity.Version} from {source.Source}", e);
            }
        }

        private async Task<(PackageSource, IPackageSearchMetadata)> GetLatestVersionInternalAsync(
            string packageIdentifier,
            IEnumerable<PackageSource> packageSources,
            ITestOutputHelper? log = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            _ = packageSources ?? throw new ArgumentNullException(nameof(packageSources));

            (PackageSource Source, IEnumerable<IPackageSearchMetadata>? FoundPackages)[] foundPackagesBySource =
                await Task.WhenAll(
                    packageSources.Select(source => GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, log, cancellationToken)))
                          .ConfigureAwait(false);

            if (!foundPackagesBySource.Where(result => result.FoundPackages != null).Any())
            {
                throw new Exception($"Failed to load NuGet sources {string.Join(";", packageSources.Select(source => source.Source))}");
            }

            var accumulativeSearchResults = foundPackagesBySource
                .Where(r => r.FoundPackages != null)
                .SelectMany(result => result.FoundPackages!.Select(package => (result.Source, package)));

            if (!accumulativeSearchResults.Any())
            {
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
            return latestVersion;
        }

        private async Task<(PackageSource, IPackageSearchMetadata)> GetPackageMetadataAsync(
            string packageIdentifier,
            NuGetVersion packageVersion,
            IEnumerable<PackageSource> sources,
            ITestOutputHelper? log = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            _ = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
            _ = sources ?? throw new ArgumentNullException(nameof(sources));

            bool atLeastOneSourceValid = false;
            using CancellationTokenSource linkedCts =
                      CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tasks = sources.Select(source => GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, log, linkedCts.Token)).ToList();
            while (tasks.Any())
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);
                (PackageSource Source, IEnumerable<IPackageSearchMetadata> FoundPackages) result = await finishedTask.ConfigureAwait(false);
                if (result.FoundPackages == null)
                {
                    continue;
                }
                atLeastOneSourceValid = true;
                IPackageSearchMetadata? matchedVersion = result.FoundPackages!.FirstOrDefault(package => package.Identity.Version == packageVersion);
                if (matchedVersion != null)
                {
                    linkedCts.Cancel();
                    return (result.Source, matchedVersion);
                }
            }
            if (!atLeastOneSourceValid)
            {
                throw new Exception($"Failed to load NuGet sources {string.Join(";", sources.Select(source => source.Source))}");
            }
            throw new Exception($"{packageIdentifier}, version: {packageVersion} is not found in {string.Join(";", sources.Select(source => source.Source))}");
        }

        private async Task<(PackageSource Source, IEnumerable<IPackageSearchMetadata>? FoundPackages)> GetPackageMetadataAsync(
            PackageSource source,
            string packageIdentifier,
            bool includePrerelease = false,
            ITestOutputHelper? log = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            _ = source ?? throw new ArgumentNullException(nameof(source));

            try
            {
                SourceRepository repository = Repository.Factory.GetCoreV3(source);
                PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken).ConfigureAwait(false);
                IEnumerable<IPackageSearchMetadata> foundPackages = await resource.GetMetadataAsync(
                    packageIdentifier,
                    includePrerelease: includePrerelease,
                    includeUnlisted: false,
                    _cacheSettings,
                    _nugetLogger,
                    cancellationToken).ConfigureAwait(false);
                return (source, foundPackages);
            }
            catch (Exception ex)
            {
                //ignore errors
                log?.WriteLine($"Retrieving info from {source.Source} failed, details: {ex}");
                return (source, null);
            }
        }

        private IEnumerable<PackageSource> LoadNuGetSources(params string[] additionalSources)
        {
            IEnumerable<PackageSource> defaultSources;
            string currentDirectory = string.Empty;
            try
            {
                currentDirectory = Directory.GetCurrentDirectory();
                ISettings settings = global::NuGet.Configuration.Settings.LoadDefaultSettings(currentDirectory);
                PackageSourceProvider packageSourceProvider = new PackageSourceProvider(settings);
                defaultSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load NuGet sources configured for the folder {currentDirectory}", ex);
            }

            if (!additionalSources.Any())
            {
                if (!defaultSources.Any())
                {
                    throw new Exception("No NuGet sources are defined or enabled");
                }
                return defaultSources;
            }

            List<PackageSource> customSources = new List<PackageSource>();
            foreach (string source in additionalSources)
            {
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
                    continue;
                }
                customSources.Add(packageSource);
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
