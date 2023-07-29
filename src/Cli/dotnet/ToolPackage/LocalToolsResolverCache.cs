// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Text.Json;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    internal class LocalToolsResolverCache : ILocalToolsResolverCache
    {
        private readonly DirectoryPath _cacheVersionedDirectory;
        private readonly IFileSystem _fileSystem;
        private const int LocalToolResolverCacheVersion = 1;

        public LocalToolsResolverCache(IFileSystem fileSystem = null,
            DirectoryPath? cacheDirectory = null,
            int version = LocalToolResolverCacheVersion)
        {
            _fileSystem = fileSystem ?? new FileSystemWrapper();
            DirectoryPath appliedCacheDirectory =
                cacheDirectory ?? new DirectoryPath(Path.Combine(CliFolderPathCalculator.ToolsResolverCachePath));
            _cacheVersionedDirectory = appliedCacheDirectory.WithSubDirectories(version.ToString());
        }

        public void Save(
            IDictionary<RestoredCommandIdentifier, RestoredCommand> restoredCommandMap)
        {
            EnsureFileStorageExists();

            foreach (var distinctPackageIdAndRestoredCommandMap in restoredCommandMap.GroupBy(x => x.Key.PackageId))
            {
                PackageId distinctPackageId = distinctPackageIdAndRestoredCommandMap.Key;
                string packageCacheFile = GetCacheFile(distinctPackageId);
                if (_fileSystem.File.Exists(packageCacheFile))
                {
                    var existingCacheTable = GetCacheTable(packageCacheFile);

                    var diffedRow = distinctPackageIdAndRestoredCommandMap
                        .Where(pair => !TryGetMatchingRestoredCommand(
                            pair.Key,
                            existingCacheTable, out _))
                        .Select(pair => ConvertToCacheRow(pair.Key, pair.Value));

                    _fileSystem.File.WriteAllText(
                        packageCacheFile,
                        JsonSerializer.Serialize(existingCacheTable.Concat(diffedRow)));
                }
                else
                {
                    var rowsToAdd =
                        distinctPackageIdAndRestoredCommandMap
                            .Select(mapWithSamePackageId
                                => ConvertToCacheRow(
                                    mapWithSamePackageId.Key,
                                    mapWithSamePackageId.Value));

                    _fileSystem.File.WriteAllText(
                        packageCacheFile,
                        JsonSerializer.Serialize(rowsToAdd));
                }
            }
        }

        public bool TryLoad(
            RestoredCommandIdentifier restoredCommandIdentifier,
            out RestoredCommand restoredCommand)
        {
            string packageCacheFile = GetCacheFile(restoredCommandIdentifier.PackageId);
            if (_fileSystem.File.Exists(packageCacheFile))
            {
                if (TryGetMatchingRestoredCommand(
                    restoredCommandIdentifier,
                    GetCacheTable(packageCacheFile),
                    out restoredCommand))
                {
                    return true;
                }
            }

            restoredCommand = null;
            return false;
        }

        private CacheRow[] GetCacheTable(string packageCacheFile)
        {
            CacheRow[] cacheTable = Array.Empty<CacheRow>();

            try
            {
                cacheTable =
                    JsonSerializer.Deserialize<CacheRow[]>(_fileSystem.File.ReadAllText(packageCacheFile));
            }
            catch (JsonException)
            {
                // if file is corrupted, treat it as empty since it is not the source of truth
            }

            return cacheTable;
        }

        public bool TryLoadHighestVersion(
            RestoredCommandIdentifierVersionRange query,
            out RestoredCommand restoredCommandList)
        {
            restoredCommandList = null;
            string packageCacheFile = GetCacheFile(query.PackageId);
            if (_fileSystem.File.Exists(packageCacheFile))
            {
                var list = GetCacheTable(packageCacheFile)
                    .Select(c => Convert(query.PackageId, c))
                    .Where(strongTypeStored =>
                        query.VersionRange.Satisfies(strongTypeStored.restoredCommandIdentifier.Version))
                    .Where(onlyVersionSatisfies =>
                        onlyVersionSatisfies.restoredCommandIdentifier ==
                        query.WithVersion(onlyVersionSatisfies.restoredCommandIdentifier.Version))
                    .OrderByDescending(allMatched => allMatched.restoredCommandIdentifier.Version)
                    .FirstOrDefault();

                if (!list.restoredCommand.Equals(default(RestoredCommand)))
                {
                    restoredCommandList = list.restoredCommand;
                    return true;
                }
            }

            return false;
        }

        private string GetCacheFile(PackageId packageId)
        {
            return _cacheVersionedDirectory.WithFile(packageId.ToString()).Value;
        }

        private void EnsureFileStorageExists()
        {
            _fileSystem.Directory.CreateDirectory(_cacheVersionedDirectory.Value);
        }

        private static CacheRow ConvertToCacheRow(
            RestoredCommandIdentifier restoredCommandIdentifier,
            RestoredCommand restoredCommandList)
        {
            return new CacheRow
            {
                Version = restoredCommandIdentifier.Version.ToNormalizedString(),
                TargetFramework = restoredCommandIdentifier.TargetFramework.GetShortFolderName(),
                RuntimeIdentifier = restoredCommandIdentifier.RuntimeIdentifier.ToLowerInvariant(),
                Name = restoredCommandIdentifier.CommandName.Value,
                Runner = restoredCommandList.Runner,
                PathToExecutable = restoredCommandList.Executable.Value
            };
        }

        private static
            (RestoredCommandIdentifier restoredCommandIdentifier,
            RestoredCommand restoredCommand)
            Convert(
                PackageId packageId,
                CacheRow cacheRow)
        {
            RestoredCommandIdentifier restoredCommandIdentifier =
                new RestoredCommandIdentifier(
                    packageId,
                    NuGetVersion.Parse(cacheRow.Version),
                    NuGetFramework.Parse(cacheRow.TargetFramework),
                    cacheRow.RuntimeIdentifier,
                    new ToolCommandName(cacheRow.Name));

            RestoredCommand restoredCommand =
                new RestoredCommand(
                    new ToolCommandName(cacheRow.Name),
                    cacheRow.Runner,
                    new FilePath(cacheRow.PathToExecutable));

            return (restoredCommandIdentifier, restoredCommand);
        }

        private static bool TryGetMatchingRestoredCommand(
            RestoredCommandIdentifier restoredCommandIdentifier,
            CacheRow[] cacheTable,
            out RestoredCommand restoredCommandList)
        {
            (RestoredCommandIdentifier restoredCommandIdentifier, RestoredCommand restoredCommand)[]
                matchingRow = cacheTable
                    .Select(c => Convert(restoredCommandIdentifier.PackageId, c))
                    .Where(candidate => candidate.restoredCommandIdentifier == restoredCommandIdentifier).ToArray();

            if (matchingRow.Length >= 2)
            {
                throw new ResolverCacheInconsistentException(
                    $"more than one row for {restoredCommandIdentifier.DebugToString()}");
            }

            if (matchingRow.Length == 1)
            {
                restoredCommandList = matchingRow[0].restoredCommand;
                return true;
            }

            restoredCommandList = null;
            return false;
        }

        private class CacheRow
        {
            public string Version { get; set; }
            public string TargetFramework { get; set; }
            public string RuntimeIdentifier { get; set; }
            public string Name { get; set; }
            public string Runner { get; set; }
            public string PathToExecutable { get; set; }
        }
    }
}
