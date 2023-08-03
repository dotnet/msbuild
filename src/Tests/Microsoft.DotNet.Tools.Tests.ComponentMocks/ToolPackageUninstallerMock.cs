// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ToolPackageUninstallerMock : IToolPackageUninstaller
    {
        private IToolPackageStoreQuery _toolPackageStore;
        private Action _uninstallCallback;
        private IFileSystem _fileSystem;

        public ToolPackageUninstallerMock(IFileSystem fileSystem,
            IToolPackageStoreQuery toolPackageStore,
            Action uninstallCallback = null)
        {
            _toolPackageStore = toolPackageStore ?? throw new ArgumentNullException(nameof(toolPackageStore));
            _uninstallCallback = uninstallCallback;
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public void Uninstall(DirectoryPath packageDirectory)
        {
            var rootDirectory = packageDirectory.GetParentPath();
            string tempPackageDirectory = null;

            TransactionalAction.Run(
                action: () =>
                {
                    try
                    {
                        if (_fileSystem.Directory.Exists(packageDirectory.Value))
                        {
                            string systemTempPath = Path.GetTempPath();
                            _fileSystem.Directory.CreateDirectory(systemTempPath);
                            var tempPath = Path.Combine(systemTempPath, Path.GetRandomFileName());
                            _fileSystem.Directory.Move(packageDirectory.Value, tempPath);
                            tempPackageDirectory = tempPath;
                        }

                        if (_fileSystem.Directory.Exists(rootDirectory.Value) &&
                            !_fileSystem.Directory.EnumerateFileSystemEntries(rootDirectory.Value).Any())
                        {
                            _fileSystem.Directory.Delete(rootDirectory.Value, false);
                        }

                        if (_uninstallCallback != null)
                        {
                            _uninstallCallback();
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        throw new ToolPackageException(ex.Message, ex);
                    }
                },
                commit: () =>
                {
                    if (tempPackageDirectory != null)
                    {
                        _fileSystem.Directory.Delete(tempPackageDirectory, true);
                    }
                },
                rollback: () =>
                {
                    if (tempPackageDirectory != null)
                    {
                        _fileSystem.Directory.CreateDirectory(rootDirectory.Value);
                        _fileSystem.Directory.Move(tempPackageDirectory, packageDirectory.Value);
                    }
                });
        }
    }
}
