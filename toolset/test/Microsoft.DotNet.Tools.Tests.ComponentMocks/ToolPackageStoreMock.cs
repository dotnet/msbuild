// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ToolPackageStoreMock : IToolPackageStoreQuery, IToolPackageStore
    {
        private IFileSystem _fileSystem;

        public ToolPackageStoreMock(
            DirectoryPath root,
            IFileSystem fileSystem)
        {
            Root = new DirectoryPath(Path.GetFullPath(root.Value));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public DirectoryPath Root { get; private set; }

        public DirectoryPath GetRandomStagingDirectory()
        {
            return Root.WithSubDirectories(ToolPackageStoreAndQuery.StagingDirectory, Path.GetRandomFileName());
        }

        public NuGetVersion GetStagedPackageVersion(DirectoryPath stagingDirectory, PackageId packageId)
        {
            if (NuGetVersion.TryParse(
                Path.GetFileName(
                    _fileSystem.Directory.EnumerateFileSystemEntries(
                        stagingDirectory.WithSubDirectories(packageId.ToString()).Value).FirstOrDefault()),
                out var version))
            {
                return version;
            }

            throw new ToolPackageException(
                string.Format(
                    CommonLocalizableStrings.FailedToFindStagedToolPackage,
                    packageId));
        }

        public DirectoryPath GetRootPackageDirectory(PackageId packageId)
        {
            return Root.WithSubDirectories(packageId.ToString());
        }

        public DirectoryPath GetPackageDirectory(PackageId packageId, NuGetVersion version)
        {
            return GetRootPackageDirectory(packageId)
                .WithSubDirectories(version.ToNormalizedString().ToLowerInvariant());
        }

        public IEnumerable<IToolPackage> EnumeratePackages()
        {
            if (!_fileSystem.Directory.Exists(Root.Value))
            {
                yield break;
            }

            foreach (var subdirectory in _fileSystem.Directory.EnumerateFileSystemEntries(Root.Value))
            {
                var name = Path.GetFileName(subdirectory);
                var packageId = new PackageId(name);

                if (name == ToolPackageStoreAndQuery.StagingDirectory || name != packageId.ToString())
                {
                    continue;
                }

                foreach (var package in EnumeratePackageVersions(packageId))
                {
                    yield return package;
                }
            }
        }

        public IEnumerable<IToolPackage> EnumeratePackageVersions(PackageId packageId)
        {
            var packageRootDirectory = Root.WithSubDirectories(packageId.ToString());
            if (!_fileSystem.Directory.Exists(packageRootDirectory.Value))
            {
                yield break;
            }

            foreach (var subdirectory in _fileSystem.Directory.EnumerateFileSystemEntries(packageRootDirectory.Value))
            {
                yield return new ToolPackageMock(
                    _fileSystem,
                    packageId,
                    NuGetVersion.Parse(Path.GetFileName(subdirectory)),
                    new DirectoryPath(subdirectory));
            }
        }

        public IToolPackage GetPackage(PackageId packageId, NuGetVersion version)
        {
            var directory = GetPackageDirectory(packageId, version);
            if (!_fileSystem.Directory.Exists(directory.Value))
            {
                return null;
            }
            return new ToolPackageMock(_fileSystem, packageId, version, directory);
        }
    }
}
