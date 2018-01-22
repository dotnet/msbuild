// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Install.Tool
{
    public class InstallToolCommand : CommandBase
    {
        private static string _packageId;
        private static string _packageVersion;
        private static string _configFilePath;
        private static string _framework;
        private static string _source;
        private static bool _global;

        public InstallToolCommand(
            AppliedOption appliedCommand,
            ParseResult parseResult)
            : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }

            _packageId = appliedCommand.Arguments.Single();
            _packageVersion = appliedCommand.ValueOrDefault<string>("version");
            _configFilePath = appliedCommand.ValueOrDefault<string>("configfile");
            _framework = appliedCommand.ValueOrDefault<string>("framework");
            _source = appliedCommand.ValueOrDefault<string>("source");
            _global = appliedCommand.ValueOrDefault<bool>("global");
        }

        public override int Execute()
        {
            if (!_global)
            {
                throw new GracefulException(LocalizableStrings.InstallToolCommandOnlySupportGlobal);
            }

            var cliFolderPathCalculator = new CliFolderPathCalculator();
            var offlineFeedPath = new DirectoryPath(cliFolderPathCalculator.CliFallbackFolderPath);

            var toolConfigurationAndExecutablePath = ObtainPackage(
                executablePackagePath: new DirectoryPath(cliFolderPathCalculator.ToolsPackagePath),
                offlineFeedPath: offlineFeedPath);

            var shellShimMaker = new ShellShimMaker(cliFolderPathCalculator.ToolsShimPath);
            var commandName = toolConfigurationAndExecutablePath.Configuration.CommandName;
            shellShimMaker.EnsureCommandNameUniqueness(commandName);

            shellShimMaker.CreateShim(
                toolConfigurationAndExecutablePath.Executable.Value,
                commandName);

            EnvironmentPathFactory
                .CreateEnvironmentPathInstruction()
                .PrintAddPathInstructionIfPathDoesNotExist();

            Reporter.Output.WriteLine(
                string.Format(LocalizableStrings.InstallationSucceeded, commandName));

            return 0;
        }

        private static ToolConfigurationAndExecutablePath ObtainPackage(
            DirectoryPath executablePackagePath,
            DirectoryPath offlineFeedPath)
        {
            try
            {
                FilePath? configFile = null;
                if (_configFilePath != null)
                {
                    configFile = new FilePath(_configFilePath);
                }

                var toolPackageObtainer =
                    new ToolPackageObtainer(
                        executablePackagePath,
                        offlineFeedPath,
                        () => new DirectoryPath(Path.GetTempPath())
                            .WithSubDirectories(Path.GetRandomFileName())
                            .WithFile(Path.GetRandomFileName() + ".csproj"),
                        new Lazy<string>(BundledTargetFramework.GetTargetFrameworkMoniker),
                        new PackageToProjectFileAdder(),
                        new ProjectRestorer());

                return toolPackageObtainer.ObtainAndReturnExecutablePath(
                    packageId: _packageId,
                    packageVersion: _packageVersion,
                    nugetconfig: configFile,
                    targetframework: _framework,
                    source: _source);
            }

            catch (PackageObtainException ex)
            {
                throw new GracefulException(
                    message:
                    string.Format(LocalizableStrings.InstallFailedNuget,
                        ex.Message),
                    innerException: ex);
            }
            catch (ToolConfigurationException ex)
            {
                throw new GracefulException(
                    message:
                    string.Format(
                        LocalizableStrings.InstallFailedPackage,
                        ex.Message),
                    innerException: ex);
            }
        }
    }
}
