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
    internal class ToolPackageMock : IToolPackage
    {
        private IFileSystem _fileSystem;
        private Lazy<IReadOnlyList<CommandSettings>> _commands;
        private IEnumerable<string> _warnings;
        private readonly IReadOnlyList<FilePath> _packagedShims;

        public ToolPackageMock(
            IFileSystem fileSystem,
            PackageId id,
            NuGetVersion version,
            DirectoryPath packageDirectory,
            IEnumerable<string> warnings = null,
            IReadOnlyList<FilePath> packagedShims = null)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            Id = id;
            Version = version ?? throw new ArgumentNullException(nameof(version));
            PackageDirectory = packageDirectory;
            _commands = new Lazy<IReadOnlyList<CommandSettings>>(GetCommands);
            _warnings = warnings ?? new List<string>();
            _packagedShims = packagedShims ?? new List<FilePath>();
        }

        public PackageId Id { get; private set; }

        public NuGetVersion Version { get; private set; }

        public DirectoryPath PackageDirectory { get; private set; }

        public IReadOnlyList<CommandSettings> Commands
        {
            get
            {
                return _commands.Value;
            }
        }

        public IEnumerable<string> Warnings => _warnings;

        public IReadOnlyList<FilePath> PackagedShims
        {
            get
            {
                return _packagedShims;
            }
        }

        private IReadOnlyList<CommandSettings> GetCommands()
        {
            try
            {
                // The mock restorer wrote the path to the executable into project.assets.json (not a real assets file)
                // Currently only "dotnet" commands are supported
                var executablePath = _fileSystem.File.ReadAllText(Path.Combine(PackageDirectory.Value, "project.assets.json"));
                return new CommandSettings[]
                {
                    new CommandSettings(
                        ProjectRestorerMock.FakeCommandName,
                        "dotnet",
                        PackageDirectory.WithFile(executablePath))
                };
            }
            catch (IOException ex)
            {
                throw new ToolPackageException(
                    string.Format(
                        CommonLocalizableStrings.FailedToRetrieveToolConfiguration,
                        Id,
                        ex.Message),
                    ex);
            }
        }
    }
}
