// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ToolPackageStoreMock : IToolPackageStore
    {
        private IFileSystem _fileSystem;
        private Action _uninstallCallback;

        public ToolPackageStoreMock(
            DirectoryPath root,
            IFileSystem fileSystem,
            Action uninstallCallback = null)
        {
            Root = root;
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _uninstallCallback = uninstallCallback;
        }

        public DirectoryPath Root { get; private set; }

        public IEnumerable<IToolPackage> GetInstalledPackages(string packageId)
        {
            var packageRootDirectory = Root.WithSubDirectories(packageId);
            if (!_fileSystem.Directory.Exists(packageRootDirectory.Value))
            {
                yield break;
            }

            foreach (var subdirectory in _fileSystem.Directory.EnumerateFileSystemEntries(packageRootDirectory.Value))
            {
                var version = Path.GetFileName(subdirectory);
                yield return new ToolPackageMock(
                    _fileSystem,
                    packageId,
                    version,
                    packageRootDirectory.WithSubDirectories(version),
                    _uninstallCallback);
            }
        }
    }
}
