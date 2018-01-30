// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ShellShimMakerMock : IShellShimMaker
    {
        private static IFileSystem _fileSystem;
        private readonly string _pathToPlaceShim;

        public ShellShimMakerMock(string pathToPlaceShim, IFileSystem fileSystem = null)
        {
            _pathToPlaceShim =
                pathToPlaceShim ?? throw new ArgumentNullException(nameof(pathToPlaceShim));

            _fileSystem = fileSystem ?? new FileSystemWrapper();
        }

        public void CreateShim(FilePath packageExecutable, string shellCommandName)
        {
            var fakeshim = new FakeShim
            {
                Runner = "dotnet",
                ExecutablePath = packageExecutable.Value
            };
            var script = JsonConvert.SerializeObject(fakeshim);

            FilePath scriptPath = new FilePath(Path.Combine(_pathToPlaceShim, shellCommandName));
            _fileSystem.File.WriteAllText(scriptPath.Value, script);
        }

        public void EnsureCommandNameUniqueness(string shellCommandName)
        {
            if (_fileSystem.File.Exists(Path.Combine(_pathToPlaceShim, shellCommandName)))
            {
                throw new GracefulException(
                    string.Format(CommonLocalizableStrings.FailInstallToolSameName,
                        shellCommandName));
            }
        }

        public class FakeShim
        {
            public string Runner { get; set; }
            public string ExecutablePath { get; set; }
        }
    }
}
