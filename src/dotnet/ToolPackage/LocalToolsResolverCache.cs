// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    internal class LocalToolsResolverCache
    {
        private readonly DirectoryPath _cacheVersionedDirectory;
        private readonly IFileSystem _fileSystem;

        public LocalToolsResolverCache(IFileSystem fileSystem, DirectoryPath cacheDirectory, int version)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _cacheVersionedDirectory = cacheDirectory.WithSubDirectories(version.ToString());
        }

        public void Save(
            IDictionary<RestoreCommandIdentifier, RestoredCommand> commandSettingsMap,
            DirectoryPath nuGetGlobalPackagesFolder)
        {
            EnsureFileStorageExists();

            foreach (var distinctPackageIdAndPair in commandSettingsMap.GroupBy(x => x.Key.PackageId))
            {
                PackageId distinctPackageId = distinctPackageIdAndPair.Key;

                string packageCacheFile = GetCacheFile(distinctPackageId);
                if (_fileSystem.File.Exists(packageCacheFile))
                {
                    var existingCacheTable = GetCacheTable(packageCacheFile);

                    var diffedRow = distinctPackageIdAndPair
                        .Where(pair => !TryGetMatchingCommandSettingsList(
                            pair.Key,
                            nuGetGlobalPackagesFolder,
                            existingCacheTable, out _))
                        .Select(pair => ConvertToCacheRow(pair.Key, pair.Value, nuGetGlobalPackagesFolder));

                    _fileSystem.File.WriteAllText(
                        packageCacheFile,
                        JsonConvert.SerializeObject(existingCacheTable.Concat(diffedRow)));
                }
                else
                {
                    var rowsToAdd =
                        distinctPackageIdAndPair
                            .Select(x => ConvertToCacheRow(x.Key, x.Value, nuGetGlobalPackagesFolder));

                    _fileSystem.File.WriteAllText(
                        packageCacheFile,
                        JsonConvert.SerializeObject(rowsToAdd));
                }
            }
        }

        public bool TryLoad(
            RestoreCommandIdentifier restoreCommandIdentifier,
            DirectoryPath nuGetGlobalPackagesFolder,
            out RestoredCommand restoredCommand)
        {
            string packageCacheFile = GetCacheFile(restoreCommandIdentifier.PackageId);
            if (_fileSystem.File.Exists(packageCacheFile))
            {
                if (TryGetMatchingCommandSettingsList(
                    restoreCommandIdentifier,
                    nuGetGlobalPackagesFolder,
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
                    JsonConvert.DeserializeObject<CacheRow[]>(_fileSystem.File.ReadAllText(packageCacheFile));
            }
            catch (JsonReaderException)
            {
                // if file is corrupted, treat it as empty since it is not the source of truth
            }

            return cacheTable;
        }

        public bool TryLoadHighestVersion(
            CommandSettingsListIdVersionRange query,
            DirectoryPath nuGetGlobalPackagesFolder,
            out RestoredCommand restoredCommandList)
        {
            restoredCommandList = null;
            string packageCacheFile = GetCacheFile(query.PackageId);
            if (_fileSystem.File.Exists(packageCacheFile))
            {
                var list = GetCacheTable(packageCacheFile)
                    .Select(c => Convert(query.PackageId, c, nuGetGlobalPackagesFolder))
                    .Where(strongTypeStored =>
                        query.VersionRange.Satisfies(strongTypeStored.commandSettingsListId.Version))
                    .Where(onlyVersionSatisfies =>
                        onlyVersionSatisfies.commandSettingsListId ==
                        query.WithVersion(onlyVersionSatisfies.commandSettingsListId.Version))
                    .OrderByDescending(allMatched => allMatched.commandSettingsListId.Version)
                    .FirstOrDefault();

                if (!list.commandSettingsList.Equals(default(RestoredCommand)))
                {
                    restoredCommandList = list.commandSettingsList;
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
            RestoreCommandIdentifier restoreCommandIdentifier,
            RestoredCommand restoredCommandList,
            DirectoryPath nuGetGlobalPackagesFolder)
        {
            return new CacheRow
            {
                Version = restoreCommandIdentifier.Version.ToNormalizedString(),
                TargetFramework = restoreCommandIdentifier.TargetFramework.GetShortFolderName(),
                RuntimeIdentifier = restoreCommandIdentifier.RuntimeIdentifier.ToLowerInvariant(),
                Name = restoreCommandIdentifier.CommandName,
                Runner = restoredCommandList.Runner,
                RelativeToNuGetGlobalPackagesFolderPathToDll =
                    Path.GetRelativePath(nuGetGlobalPackagesFolder.Value, restoredCommandList.Executable.Value)
            };
        }

        private static
            (RestoreCommandIdentifier commandSettingsListId,
            RestoredCommand commandSettingsList)
            Convert(
                PackageId packageId,
                CacheRow cacheRow,
                DirectoryPath nuGetGlobalPackagesFolder)
        {
            RestoreCommandIdentifier restoreCommandIdentifier = new RestoreCommandIdentifier(
                packageId,
                NuGetVersion.Parse(cacheRow.Version),
                NuGetFramework.Parse(cacheRow.TargetFramework),
                cacheRow.RuntimeIdentifier,
                cacheRow.Name);

            RestoredCommand restoredCommand = new RestoredCommand(cacheRow.Name, cacheRow.Runner,
                nuGetGlobalPackagesFolder
                    .WithFile(cacheRow.RelativeToNuGetGlobalPackagesFolderPathToDll));

            return (restoreCommandIdentifier, restoredCommand);
        }

        private static bool TryGetMatchingCommandSettingsList(
            RestoreCommandIdentifier restoreCommandIdentifier,
            DirectoryPath nuGetGlobalPackagesFolder,
            CacheRow[] cacheTable,
            out RestoredCommand restoredCommandList)
        {
            (RestoreCommandIdentifier commandSettingsListId, RestoredCommand commandSettingsList)[]
                matchingRow = cacheTable
                    .Select(c => Convert(restoreCommandIdentifier.PackageId, c, nuGetGlobalPackagesFolder))
                    .Where(candidate => candidate.commandSettingsListId == restoreCommandIdentifier).ToArray();

            if (matchingRow.Length >= 2)
            {
                throw new ResolverCacheInconsistentException(
                    $"more than one row for {restoreCommandIdentifier.DebugToString()}");
            }

            if (matchingRow.Length == 1)
            {
                restoredCommandList = matchingRow[0].commandSettingsList;
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
            public string RelativeToNuGetGlobalPackagesFolderPathToDll { get; set; }
        }
    }
}
