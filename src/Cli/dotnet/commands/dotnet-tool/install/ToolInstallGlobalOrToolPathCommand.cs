// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal delegate IShellShimRepository CreateShellShimRepository(string appHostSourceDirectory, DirectoryPath? nonGlobalLocation = null);
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
        private readonly ShellShimTemplateFinder _shellShimTemplateFinder;

        private readonly PackageId _packageId;
        private readonly string _packageVersion;
        private readonly string _configFilePath;
        private readonly string _framework;
        private readonly string[] _source;
        private readonly bool _global;
        private readonly string _verbosity;
        private readonly string _toolPath;
        private readonly string _architectureOption;
        private IEnumerable<string> _forwardRestoreArguments;

        public ToolInstallGlobalOrToolPathCommand(
            ParseResult parseResult,
            CreateToolPackageStoresAndInstaller createToolPackageStoreAndInstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IEnvironmentPathInstruction environmentPathInstruction = null,
            IReporter reporter = null,
            INuGetPackageDownloader nugetPackageDownloader = null)
            : base(parseResult)
        {
            _packageId = new PackageId(parseResult.GetValueForArgument(ToolInstallCommandParser.PackageIdArgument));
            _packageVersion = parseResult.GetValueForOption(ToolInstallCommandParser.VersionOption);
            _configFilePath = parseResult.GetValueForOption(ToolInstallCommandParser.ConfigOption);
            _framework = parseResult.GetValueForOption(ToolInstallCommandParser.FrameworkOption);
            _source = parseResult.GetValueForOption(ToolInstallCommandParser.AddSourceOption);
            _global = parseResult.GetValueForOption(ToolAppliedOption.GlobalOption);
            _verbosity = Enum.GetName(parseResult.GetValueForOption(ToolInstallCommandParser.VerbosityOption));
            _toolPath = parseResult.GetValueForOption(ToolAppliedOption.ToolPathOption);
            _architectureOption = parseResult.GetValueForOption(ToolInstallCommandParser.ArchitectureOption);

            _createToolPackageStoresAndInstaller = createToolPackageStoreAndInstaller ?? ToolPackageFactory.CreateToolPackageStoresAndInstaller;
            _forwardRestoreArguments = parseResult.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand());

            _environmentPathInstruction = environmentPathInstruction
                ?? EnvironmentPathFactory.CreateEnvironmentPathInstruction();
            _createShellShimRepository = createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;
            var tempDir = new DirectoryPath(Path.Combine(Path.GetTempPath(), "dotnet-tool-install"));
            var configOption = parseResult.GetValueForOption(ToolInstallCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValueForOption(ToolInstallCommandParser.AddSourceOption);
            var packageSourceLocation = new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), additionalSourceFeeds: sourceOption);
            var restoreAction = new RestoreActionConfig(DisableParallel: parseResult.GetValueForOption(ToolCommandRestorePassThroughOptions.DisableParallelOption),
                NoCache: parseResult.GetValueForOption(ToolCommandRestorePassThroughOptions.NoCacheOption),
                IgnoreFailedSources: parseResult.GetValueForOption(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption),
                Interactive: parseResult.GetValueForOption(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption));
            nugetPackageDownloader ??= new NuGetPackageDownloader(tempDir, verboseLogger: new NullLogger(), restoreActionConfig: restoreAction);
            _shellShimTemplateFinder = new ShellShimTemplateFinder(nugetPackageDownloader, tempDir, packageSourceLocation);

            _reporter = (reporter ?? Reporter.Output);
            _errorReporter = (reporter ?? Reporter.Error);
        }

        public override int Execute()
        {
            if (!string.IsNullOrEmpty(_configFilePath) && !File.Exists(_configFilePath))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.NuGetConfigurationFileDoesNotExist,
                        Path.GetFullPath(_configFilePath)));
            }

            VersionRange versionRange = _parseResult.GetVersionRange();

            DirectoryPath? toolPath = null;
            if (!string.IsNullOrEmpty(_toolPath))
            {
                toolPath = new DirectoryPath(_toolPath);
            }

            (IToolPackageStore toolPackageStore, IToolPackageStoreQuery toolPackageStoreQuery, IToolPackageInstaller toolPackageInstaller) =
                _createToolPackageStoresAndInstaller(toolPath, _forwardRestoreArguments);

            // Prevent installation if any version of the package is installed
            if (toolPackageStoreQuery.EnumeratePackageVersions(_packageId).FirstOrDefault() != null)
            {
                _errorReporter.WriteLine(string.Format(LocalizableStrings.ToolAlreadyInstalled, _packageId).Red());
                return 1;
            }

            FilePath? configFile = null;
            if (!string.IsNullOrEmpty(_configFilePath))
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

                    NuGetFramework framework;
                    if (string.IsNullOrEmpty(_framework) && package.Frameworks.Count() > 0)
                    {
                        framework = package.Frameworks
                            .Where(f => f.Version < (new NuGetVersion(Product.Version)).Version)
                            .MaxBy(f => f.Version);
                    }
                    else
                    {
                        framework = string.IsNullOrEmpty(_framework)  ?
                            null :
                            NuGetFramework.Parse(_framework);
                    }

                    string appHostSourceDirectory = _shellShimTemplateFinder.ResolveAppHostSourceDirectoryAsync(_architectureOption, framework, RuntimeInformation.ProcessArchitecture).Result;
                    IShellShimRepository shellShimRepository = _createShellShimRepository(appHostSourceDirectory, toolPath);

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
                    verboseMessages: new[] { ex.ToString() },
                    isUserError: false);
            }
        }
    }
}
