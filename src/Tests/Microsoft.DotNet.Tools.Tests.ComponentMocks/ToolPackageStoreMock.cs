// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ToolPackageStoreMock : IToolPackageStoreQuery, IToolPackageStore
    {
        private IFileSystem _fileSystem;
        private readonly Dictionary<PackageId, IEnumerable<NuGetFramework>> _frameworksMap;

        public ToolPackageStoreMock(
            DirectoryPath root,
            IFileSystem fileSystem,
            Dictionary<PackageId, IEnumerable<NuGetFramework>> frameworksMap = null)
        {
            Root = new DirectoryPath(Path.GetFullPath(root.Value));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _frameworksMap = frameworksMap ?? new Dictionary<PackageId, IEnumerable<NuGetFramework>>();
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

                IEnumerable<NuGetFramework> frameworks = null;
                _frameworksMap.TryGetValue(packageId, out frameworks);
                yield return new ToolPackageMock(
                    _fileSystem,
                    packageId,
                    NuGetVersion.Parse(Path.GetFileName(subdirectory)),
                    new DirectoryPath(subdirectory),
                    frameworks: frameworks);
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
