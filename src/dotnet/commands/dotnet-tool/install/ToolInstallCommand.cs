// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal delegate IShellShimRepository CreateShellShimRepository(DirectoryPath? nonGlobalLocation = null);
    internal delegate (IToolPackageStore, IToolPackageInstaller) CreateToolPackageStoreAndInstaller(DirectoryPath? nonGlobalLocation = null);

    internal class ToolInstallCommand : CommandBase
    {
        private readonly IEnvironmentPathInstruction _environmentPathInstruction;
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;
        private CreateShellShimRepository _createShellShimRepository;
        private CreateToolPackageStoreAndInstaller _createToolPackageStoreAndInstaller;

        private readonly PackageId _packageId;
        private readonly string _packageVersion;
        private readonly string _configFilePath;
        private readonly string _framework;
        private readonly string[] _source;
        private readonly bool _global;
        private readonly string _verbosity;
        private readonly string _toolPath;

        public ToolInstallCommand(
            AppliedOption appliedCommand,
            ParseResult parseResult,
            CreateToolPackageStoreAndInstaller createToolPackageStoreAndInstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IEnvironmentPathInstruction environmentPathInstruction = null,
            IReporter reporter = null)
            : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }

            _packageId = new PackageId(appliedCommand.Arguments.Single());
            _packageVersion = appliedCommand.ValueOrDefault<string>("version");
            _configFilePath = appliedCommand.ValueOrDefault<string>("configfile");
            _framework = appliedCommand.ValueOrDefault<string>("framework");
            _source = appliedCommand.ValueOrDefault<string[]>("source-feed");
            _global = appliedCommand.ValueOrDefault<bool>("global");
            _verbosity = appliedCommand.SingleArgumentOrDefault("verbosity");
            _toolPath = appliedCommand.SingleArgumentOrDefault("tool-path");

            var cliFolderPathCalculator = new CliFolderPathCalculator();

            _createToolPackageStoreAndInstaller = createToolPackageStoreAndInstaller ?? ToolPackageFactory.CreateToolPackageStoreAndInstaller;

            _environmentPathInstruction = environmentPathInstruction
                ?? EnvironmentPathFactory.CreateEnvironmentPathInstruction();
            _createShellShimRepository = createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;

            _reporter = (reporter ?? Reporter.Output);
            _errorReporter = (reporter ?? Reporter.Error);
        }

        public override int Execute()
        {
            if (string.IsNullOrWhiteSpace(_toolPath) && !_global)
            {
                throw new GracefulException(LocalizableStrings.InstallToolCommandNeedGlobalOrToolPath);
            }

            if (!string.IsNullOrWhiteSpace(_toolPath) && _global)
            {
                throw new GracefulException(LocalizableStrings.InstallToolCommandInvalidGlobalAndToolPath);
            }

            if (_configFilePath != null && !File.Exists(_configFilePath))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.NuGetConfigurationFileDoesNotExist,
                        Path.GetFullPath(_configFilePath)));
            }


            VersionRange versionRange = null;
            if (!string.IsNullOrEmpty(_packageVersion) && !VersionRange.TryParse(_packageVersion, out versionRange))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.InvalidNuGetVersionRange,
                        _packageVersion));
            }

            DirectoryPath? toolPath = null;
            if (_toolPath != null)
            {
                toolPath = new DirectoryPath(_toolPath);
            }

            (IToolPackageStore toolPackageStore, IToolPackageInstaller toolPackageInstaller) =
                _createToolPackageStoreAndInstaller(toolPath);
            IShellShimRepository shellShimRepository = _createShellShimRepository(toolPath);

            // Prevent installation if any version of the package is installed
            if (toolPackageStore.EnumeratePackageVersions(_packageId).FirstOrDefault() != null)
            {
                _errorReporter.WriteLine(string.Format(LocalizableStrings.ToolAlreadyInstalled, _packageId).Red());
                return 1;
            }

            FilePath? configFile = null;
            if (_configFilePath != null)
            {
                configFile = new FilePath(_configFilePath);
            }

            try
            {
                IToolPackage package = null;
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    package = toolPackageInstaller.InstallPackage(
                        packageId: _packageId,
                        versionRange: versionRange,
                        targetFramework: _framework,
                        nugetConfig: configFile,
                        additionalFeeds: _source,
                        verbosity: _verbosity);

                    foreach (var command in package.Commands)
                    {
                        shellShimRepository.CreateShim(command.Executable, command.Name);
                    }

                    scope.Complete();
                }

                if (_global)
                {
                    _environmentPathInstruction.PrintAddPathInstructionIfPathDoesNotExist();
                }

                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.InstallationSucceeded,
                        string.Join(", ", package.Commands.Select(c => c.Name)),
                        package.Id,
                        package.Version.ToNormalizedString()).Green());
                return 0;
            }
            catch (Exception ex) when (InstallToolCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
            {
                throw new GracefulException(
                    messages: InstallToolCommandLowLevelErrorConverter.GetUserFacingMessages(ex, _packageId),
                    verboseMessages: new[] {ex.ToString()},
                    isUserError: false);
            }
        }
    }
}
