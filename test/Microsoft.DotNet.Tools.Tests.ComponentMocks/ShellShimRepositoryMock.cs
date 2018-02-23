// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ShellShimRepositoryMock : IShellShimRepository
    {
        private static IFileSystem _fileSystem;
        private readonly DirectoryPath _pathToPlaceShim;

        public ShellShimRepositoryMock(DirectoryPath pathToPlaceShim, IFileSystem fileSystem = null)
        {
            _pathToPlaceShim = pathToPlaceShim;
            _fileSystem = fileSystem ?? new FileSystemWrapper();
        }

        public void CreateShim(FilePath targetExecutablePath, string commandName)
        {
            if (ShimExists(commandName))
            {
                throw new ShellShimException(
                    string.Format(CommonLocalizableStrings.ShellShimConflict,
                        commandName));
            }

            TransactionalAction.Run(
                action: () => {
                    var shim = new FakeShim
                    {
                        Runner = "dotnet",
                        ExecutablePath = targetExecutablePath.Value
                    };

                    _fileSystem.File.WriteAllText(
                        GetShimPath(commandName).Value,
                        JsonConvert.SerializeObject(shim));
                },
                rollback: () => {
                    _fileSystem.File.Delete(GetShimPath(commandName).Value);
                });
        }

        public void RemoveShim(string commandName)
        {
            var originalShimPath = GetShimPath(commandName);
            if (!_fileSystem.File.Exists(originalShimPath.Value))
            {
                return;
            }

            string tempShimPath = null;
            TransactionalAction.Run(
                action: () => {
                    var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    _fileSystem.File.Move(originalShimPath.Value, tempFile);
                    tempShimPath = tempFile;
                },
                commit: () => {
                    if (tempShimPath != null)
                    {
                        _fileSystem.File.Delete(tempShimPath);
                    }
                },
                rollback: () => {
                    if (tempShimPath != null)
                    {
                        _fileSystem.File.Move(tempShimPath, originalShimPath.Value);
                    }
                });
        }

        private bool ShimExists(string commandName)
        {
            return _fileSystem.File.Exists(GetShimPath(commandName).Value);
        }

        private FilePath GetShimPath(string shellCommandName)
        {
            var shimPath = Path.Combine(_pathToPlaceShim.Value, shellCommandName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shimPath += ".exe";
            }

            return new FilePath(shimPath);
        }

        public class FakeShim
        {
            public string Runner { get; set; }
            public string ExecutablePath { get; set; }
        }
    }
}
