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
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal delegate IShellShimRepository CreateShellShimRepository(DirectoryPath? nonGlobalLocation = null);

    internal delegate (IToolPackageStore, IToolPackageInstaller) CreateToolPackageStoreAndInstaller(
        DirectoryPath? nonGlobalLocation = null);

    internal class ToolUpdateCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;
        private readonly CreateShellShimRepository _createShellShimRepository;
        private readonly CreateToolPackageStoreAndInstaller _createToolPackageStoreAndInstaller;

        private readonly PackageId _packageId;
        private readonly string _configFilePath;
        private readonly string _framework;
        private readonly string[] _additionalFeeds;
        private readonly bool _global;
        private readonly string _verbosity;
        private readonly string _toolPath;

        public ToolUpdateCommand(AppliedOption appliedCommand,
            ParseResult parseResult,
            CreateToolPackageStoreAndInstaller createToolPackageStoreAndInstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IReporter reporter = null)
            : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }

            _packageId = new PackageId(appliedCommand.Arguments.Single());
            _configFilePath = appliedCommand.ValueOrDefault<string>("configfile");
            _framework = appliedCommand.ValueOrDefault<string>("framework");
            _additionalFeeds = appliedCommand.ValueOrDefault<string[]>("add-source");
            _global = appliedCommand.ValueOrDefault<bool>("global");
            _verbosity = appliedCommand.SingleArgumentOrDefault("verbosity");
            _toolPath = appliedCommand.SingleArgumentOrDefault("tool-path");

            _createToolPackageStoreAndInstaller = createToolPackageStoreAndInstaller ??
                                                  ToolPackageFactory.CreateToolPackageStoreAndInstaller;

            _createShellShimRepository =
                createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;

            _reporter = (reporter ?? Reporter.Output);
            _errorReporter = (reporter ?? Reporter.Error);
        }

        public override int Execute()
        {
            ValidateArguments();

            DirectoryPath? toolPath = null;
            if (_toolPath != null)
            {
                toolPath = new DirectoryPath(_toolPath);
            }

            (IToolPackageStore toolPackageStore, IToolPackageInstaller toolPackageInstaller) =
                _createToolPackageStoreAndInstaller(toolPath);
            IShellShimRepository shellShimRepository = _createShellShimRepository(toolPath);


            IToolPackage oldPackage;
            try
            {
                oldPackage = toolPackageStore.EnumeratePackageVersions(_packageId).SingleOrDefault();
                if (oldPackage == null)
                {
                    throw new GracefulException(
                        messages: new[]
                        {
                            string.Format(
                                LocalizableStrings.ToolNotInstalled,
                                _packageId),
                        },
                        isUserError: false);
                }
            }
            catch (InvalidOperationException)
            {
                throw new GracefulException(
                    messages: new[]
                    {
                        string.Format(
                            LocalizableStrings.ToolHasMultipleVersionsInstalled,
                            _packageId),
                    },
                    isUserError: false);
            }

            FilePath? configFile = null;
            if (_configFilePath != null)
            {
                configFile = new FilePath(_configFilePath);
            }

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                RunWithHandlingUninstallError(() =>
                {
                    foreach (CommandSettings command in oldPackage.Commands)
                    {
                        shellShimRepository.RemoveShim(command.Name);
                    }

                    oldPackage.Uninstall();
                });

                RunWithHandlingInstallError(() =>
                {
                    IToolPackage newInstalledPackage = toolPackageInstaller.InstallPackage(
                        packageId: _packageId,
                        targetFramework: _framework,
                        nugetConfig: configFile,
                        additionalFeeds: _additionalFeeds,
                        verbosity: _verbosity);

                    foreach (CommandSettings command in newInstalledPackage.Commands)
                    {
                        shellShimRepository.CreateShim(command.Executable, command.Name);
                    }

                    PrintSuccessMessage(oldPackage, newInstalledPackage);
                });

                scope.Complete();
            }

            return 0;
        }

        private void ValidateArguments()
        {
            if (string.IsNullOrWhiteSpace(_toolPath) && !_global)
            {
                throw new GracefulException(
                    LocalizableStrings.UpdateToolCommandNeedGlobalOrToolPath);
            }

            if (!string.IsNullOrWhiteSpace(_toolPath) && _global)
            {
                throw new GracefulException(
                    LocalizableStrings.UpdateToolCommandInvalidGlobalAndToolPath);
            }

            if (_configFilePath != null && !File.Exists(_configFilePath))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.NuGetConfigurationFileDoesNotExist,
                        Path.GetFullPath(_configFilePath)));
            }
        }

        private void RunWithHandlingInstallError(Action installAction)
        {
            try
            {
                installAction();
            }
            catch (Exception ex)
                when (InstallToolCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
            {
                var message = new List<string>
                {
                    string.Format(LocalizableStrings.UpdateToolFailed, _packageId)
                };
                message.AddRange(
                    InstallToolCommandLowLevelErrorConverter.GetUserFacingMessages(ex, _packageId));


                throw new GracefulException(
                    messages: message,
                    verboseMessages: new[] { ex.ToString() },
                    isUserError: false);
            }
        }

        private void RunWithHandlingUninstallError(Action uninstallAction)
        {
            try
            {
                uninstallAction();
            }
            catch (Exception ex)
                when (ToolUninstallCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
            {
                var message = new List<string>
                {
                    string.Format(LocalizableStrings.UpdateToolFailed, _packageId)
                };
                message.AddRange(
                    ToolUninstallCommandLowLevelErrorConverter.GetUserFacingMessages(ex, _packageId));

                throw new GracefulException(
                    messages: message,
                    verboseMessages: new[] { ex.ToString() },
                    isUserError: false);
            }
        }

        private void PrintSuccessMessage(IToolPackage oldPackage, IToolPackage newInstalledPackage)
        {
            if (oldPackage.Version != newInstalledPackage.Version)
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.UpdateSucceeded,
                        newInstalledPackage.Id,
                        oldPackage.Version.ToNormalizedString(),
                        newInstalledPackage.Version.ToNormalizedString()).Green());
            }
            else
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.UpdateSucceededVersionNoChange,
                        newInstalledPackage.Id, newInstalledPackage.Version).Green());
            }
        }
    }
}
