// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal delegate IShellShimRepository CreateShellShimRepository(DirectoryPath? nonGlobalLocation = null);
    internal delegate (IToolPackageStore, IToolPackageStoreQuery, IToolPackageInstaller) CreateToolPackageStoresAndInstaller(
		DirectoryPath? nonGlobalLocation = null,
		IEnumerable<string> forwardRestoreArguments = null);

    internal class ToolInstallGlobalOrToolPathCommand : CommandBase
    {
        private readonly IEnvironmentPathInstruction _environmentPathInstruction;
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;
        private CreateShellShimRepository _createShellShimRepository;
        private CreateToolPackageStoresAndInstaller _createToolPackageStoresAndInstaller;

        private readonly PackageId _packageId;
        private readonly string _packageVersion;
        private readonly string _configFilePath;
        private readonly string _framework;
        private readonly string[] _source;
        private readonly bool _global;
        private readonly string _verbosity;
        private readonly string _toolPath;
        private IEnumerable<string> _forwardRestoreArguments;

        public ToolInstallGlobalOrToolPathCommand(
            ParseResult parseResult,
            CreateToolPackageStoresAndInstaller createToolPackageStoreAndInstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IEnvironmentPathInstruction environmentPathInstruction = null,
            IReporter reporter = null)
            : base(parseResult)
        {
            _packageId = new PackageId(parseResult.ValueForArgument<string>(ToolInstallCommandParser.PackageIdArgument));
            _packageVersion = parseResult.ValueForOption<string>(ToolInstallCommandParser.VersionOption);
            _configFilePath = parseResult.ValueForOption<string>(ToolInstallCommandParser.ConfigOption);
            _framework = parseResult.ValueForOption<string>(ToolInstallCommandParser.FrameworkOption);
            _source = parseResult.ValueForOption<string[]>(ToolInstallCommandParser.AddSourceOption);
            _global = parseResult.ValueForOption<bool>(ToolAppliedOption.GlobalOptionAliases.First());
            _verbosity = Enum.GetName(parseResult.ValueForOption<VerbosityOptions>(ToolInstallCommandParser.VerbosityOption));
            _toolPath = parseResult.ValueForOption<string>(ToolAppliedOption.ToolPathOptionAlias);

            _createToolPackageStoresAndInstaller = createToolPackageStoreAndInstaller ?? ToolPackageFactory.CreateToolPackageStoresAndInstaller;
			_forwardRestoreArguments = parseResult.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand());

            _environmentPathInstruction = environmentPathInstruction
                ?? EnvironmentPathFactory.CreateEnvironmentPathInstruction();
            _createShellShimRepository = createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;

            _reporter = (reporter ?? Reporter.Output);
            _errorReporter = (reporter ?? Reporter.Error);
        }

        public override int Execute()
        {
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

            (IToolPackageStore toolPackageStore, IToolPackageStoreQuery toolPackageStoreQuery, IToolPackageInstaller toolPackageInstaller) =
                _createToolPackageStoresAndInstaller(toolPath, _forwardRestoreArguments);
            IShellShimRepository shellShimRepository = _createShellShimRepository(toolPath);

            // Prevent installation if any version of the package is installed
            if (toolPackageStoreQuery.EnumeratePackageVersions(_packageId).FirstOrDefault() != null)
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
                        new PackageLocation(nugetConfig: configFile, additionalFeeds: _source),
                        packageId: _packageId,
                        versionRange: versionRange,
                        targetFramework: _framework, verbosity: _verbosity);

                    foreach (var command in package.Commands)
                    {
                        shellShimRepository.CreateShim(command.Executable, command.Name, package.PackagedShims);
                    }

                    scope.Complete();
                }

                foreach (string w in package.Warnings)
                {
                    _reporter.WriteLine(w.Yellow());
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
