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
            CommandSettingsListId commandSettingsListId,
            IReadOnlyList<CommandSettings> commandSettingsList,
            DirectoryPath nuGetGlobalPackagesFolder)
        {
            EnsureFileStorageExists();

            CacheRow cacheRow =
                ConvertToCacheRow(commandSettingsListId, commandSettingsList, nuGetGlobalPackagesFolder);

            string packageCacheFile = GetCacheFile(commandSettingsListId.PackageId);

            if (_fileSystem.File.Exists(packageCacheFile))
            {
                var cacheTable = GetCacheTable(packageCacheFile);

                if (!TryGetMatchingCommandSettingsList(
                    commandSettingsListId,
                    nuGetGlobalPackagesFolder,
                    cacheTable,
                    out IReadOnlyList<CommandSettings> _))
                {
                    _fileSystem.File.WriteAllText(
                        packageCacheFile,
                        JsonConvert.SerializeObject(
                            cacheTable.Concat(new[] {cacheRow}).ToArray()));
                }
            }
            else
            {
                _fileSystem.File.WriteAllText(
                    packageCacheFile,
                    JsonConvert.SerializeObject(new[] {cacheRow}));
            }
        }

        public IReadOnlyList<CommandSettings> Load(
            CommandSettingsListId commandSettingsListId,
            DirectoryPath nuGetGlobalPackagesFolder)
        {
            string packageCacheFile = GetCacheFile(commandSettingsListId.PackageId);
            if (_fileSystem.File.Exists(packageCacheFile))
            {
                if (TryGetMatchingCommandSettingsList(
                    commandSettingsListId,
                    nuGetGlobalPackagesFolder,
                    GetCacheTable(packageCacheFile),
                    out IReadOnlyList<CommandSettings> commandSettingsList))
                {
                    return commandSettingsList;
                }
            }

            return Array.Empty<CommandSettings>();
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
            out IReadOnlyList<CommandSettings> commandSettingsList)
        {
            commandSettingsList = null;
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

                if (!list.commandSettingsList.Equals(default(IReadOnlyList<CommandSettings>)))
                {
                    commandSettingsList = list.commandSettingsList;
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
            CommandSettingsListId commandSettingsListId,
            IReadOnlyList<CommandSettings> commandSettingsList,
            DirectoryPath nuGetGlobalPackagesFolder)
        {
            return new CacheRow
            {
                Version = commandSettingsListId.Version.ToNormalizedString(),
                TargetFramework = commandSettingsListId.TargetFramework.GetShortFolderName(),
                RuntimeIdentifier = commandSettingsListId.RuntimeIdentifier.ToLowerInvariant(),
                SerializableCommandSettingsArray =
                    commandSettingsList.Select(s => new SerializableCommandSettings
                    {
                        Name = s.Name,
                        Runner = s.Runner,
                        RelativeToNuGetGlobalPackagesFolderPathToDll =
                            Path.GetRelativePath(nuGetGlobalPackagesFolder.Value, s.Executable.Value)
                    }).ToArray()
            };
        }

        private static
            (CommandSettingsListId commandSettingsListId,
            IReadOnlyList<CommandSettings> commandSettingsList)
            Convert(
                PackageId packageId,
                CacheRow cacheRow,
                DirectoryPath nuGetGlobalPackagesFolder)
        {
            CommandSettingsListId commandSettingsListId = new CommandSettingsListId(
                packageId,
                NuGetVersion.Parse(cacheRow.Version),
                NuGetFramework.Parse(cacheRow.TargetFramework),
                cacheRow.RuntimeIdentifier);

            IReadOnlyList<CommandSettings> commandSettingsList =
                cacheRow.SerializableCommandSettingsArray
                    .Select(
                        c => new CommandSettings(
                            c.Name,
                            c.Runner,
                            nuGetGlobalPackagesFolder
                                .WithFile(c.RelativeToNuGetGlobalPackagesFolderPathToDll))).ToArray();

            return (commandSettingsListId, commandSettingsList);
        }

        private static bool TryGetMatchingCommandSettingsList(
            CommandSettingsListId commandSettingsListId,
            DirectoryPath nuGetGlobalPackagesFolder,
            CacheRow[] cacheTable,
            out IReadOnlyList<CommandSettings> commandSettingsList)
        {
            (CommandSettingsListId commandSettingsListId,
                IReadOnlyList<CommandSettings> commandSettingsList)[]
                matchingRow = cacheTable
                    .Select(c => Convert(commandSettingsListId.PackageId, c, nuGetGlobalPackagesFolder))
                    .Where(candidate => candidate.commandSettingsListId == commandSettingsListId).ToArray();

            if (matchingRow.Length >= 2)
            {
                throw new ResolverCacheInconsistentException(
                    $"more than one row for {commandSettingsListId.DebugToString()}");
            }

            if (matchingRow.Length == 1)
            {
                commandSettingsList = matchingRow[0].commandSettingsList;
                return true;
            }

            commandSettingsList = null;
            return false;
        }

        private class CacheRow
        {
            public string Version { get; set; }
            public string TargetFramework { get; set; }
            public string RuntimeIdentifier { get; set; }
            public SerializableCommandSettings[] SerializableCommandSettingsArray { get; set; }
        }

        private class SerializableCommandSettings
        {
            public string Name { get; set; }
            public string Runner { get; set; }
            public string RelativeToNuGetGlobalPackagesFolderPathToDll { get; set; }
        }
    }
}
