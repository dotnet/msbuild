// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ToolPackageInstallerMock : IToolPackageInstaller
    {
        private const string ProjectFileName = "TempProject.csproj";

        private readonly IToolPackageStore _store;
        private readonly IProjectRestorer _projectRestorer;
        private readonly IFileSystem _fileSystem;
        private readonly Action _installCallback;
        private readonly Dictionary<PackageId, IEnumerable<string>> _warningsMap;
        private readonly Dictionary<PackageId, IReadOnlyList<FilePath>> _packagedShimsMap;

        public ToolPackageInstallerMock(
            IFileSystem fileSystem,
            IToolPackageStore store,
            IProjectRestorer projectRestorer,
            Action installCallback = null,
            Dictionary<PackageId, IEnumerable<string>> warningsMap = null,
            Dictionary<PackageId, IReadOnlyList<FilePath>> packagedShimsMap = null)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _projectRestorer = projectRestorer ?? throw new ArgumentNullException(nameof(projectRestorer));
            _installCallback = installCallback;
            _warningsMap = warningsMap ?? new Dictionary<PackageId, IEnumerable<string>>();
            _packagedShimsMap = packagedShimsMap ?? new Dictionary<PackageId, IReadOnlyList<FilePath>>();
        }

        public IToolPackage InstallPackage(PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            FilePath? nugetConfig = null,
            DirectoryPath? rootConfigDirectory = null,
            string[] additionalFeeds = null,
            string verbosity = null)
        {
            var packageRootDirectory = _store.GetRootPackageDirectory(packageId);
            string rollbackDirectory = null;

            return TransactionalAction.Run<IToolPackage>(
                action: () => {
                    var stageDirectory = _store.GetRandomStagingDirectory();
                    _fileSystem.Directory.CreateDirectory(stageDirectory.Value);
                    rollbackDirectory = stageDirectory.Value;

                    var tempProject = new FilePath(Path.Combine(stageDirectory.Value, ProjectFileName));

                    // Write a fake project with the requested package id, version, and framework
                    _fileSystem.File.WriteAllText(
                        tempProject.Value,
                        $"{packageId};{versionRange?.ToString("S", new VersionRangeFormatter()) ?? "*"};{targetFramework};{stageDirectory.Value}");

                    // Perform a restore on the fake project
                    _projectRestorer.Restore(
                        tempProject,
                        nugetConfig,
                        verbosity);

                    if (_installCallback != null)
                    {
                        _installCallback();
                    }

                    var version = _store.GetStagedPackageVersion(stageDirectory, packageId);
                    var packageDirectory = _store.GetPackageDirectory(packageId, version);
                    if (_fileSystem.Directory.Exists(packageDirectory.Value))
                    {
                        throw new ToolPackageException(
                            string.Format(
                                CommonLocalizableStrings.ToolPackageConflictPackageId,
                                packageId,
                                version.ToNormalizedString()));
                    }

                    _fileSystem.Directory.CreateDirectory(packageRootDirectory.Value);
                    _fileSystem.Directory.Move(stageDirectory.Value, packageDirectory.Value);
                    rollbackDirectory = packageDirectory.Value;

                    IEnumerable<string> warnings = null;
                    _warningsMap.TryGetValue(packageId, out warnings);

                    IReadOnlyList<FilePath> packedShims = null;
                    _packagedShimsMap.TryGetValue(packageId, out packedShims);

                    return new ToolPackageMock(_fileSystem, packageId, version, packageDirectory, warnings: warnings, packagedShims: packedShims);
                },
                rollback: () => {
                    if (rollbackDirectory != null && _fileSystem.Directory.Exists(rollbackDirectory))
                    {
                        _fileSystem.Directory.Delete(rollbackDirectory, true);
                    }
                    if (_fileSystem.Directory.Exists(packageRootDirectory.Value) &&
                        !_fileSystem.Directory.EnumerateFileSystemEntries(packageRootDirectory.Value).Any())
                    {
                        _fileSystem.Directory.Delete(packageRootDirectory.Value, false);
                    }
                });
        }
    }
}
