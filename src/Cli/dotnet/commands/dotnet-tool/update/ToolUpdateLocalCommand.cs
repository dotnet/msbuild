// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal class ToolUpdateLocalCommand : CommandBase
    {
        private readonly IToolManifestFinder _toolManifestFinder;
        private readonly IToolManifestEditor _toolManifestEditor;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly IToolPackageInstaller _toolPackageInstaller;
        private readonly ToolInstallLocalInstaller _toolLocalPackageInstaller;
        private readonly Lazy<ToolInstallLocalCommand> _toolInstallLocalCommand;
        private readonly IReporter _reporter;

        private readonly PackageId _packageId;
        private readonly string _explicitManifestFile;

        public ToolUpdateLocalCommand(
            ParseResult parseResult,
            IToolPackageInstaller toolPackageInstaller = null,
            IToolManifestFinder toolManifestFinder = null,
            IToolManifestEditor toolManifestEditor = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            IReporter reporter = null)
            : base(parseResult)
        {
            _packageId = new PackageId(parseResult.GetValueForArgument(ToolUpdateCommandParser.PackageIdArgument));
            _explicitManifestFile = parseResult.GetValueForOption(ToolUpdateCommandParser.ToolManifestOption);

            _reporter = (reporter ?? Reporter.Output);

            if (toolPackageInstaller == null)
            {
                (IToolPackageStore,
                    IToolPackageStoreQuery,
                    IToolPackageInstaller installer) toolPackageStoresAndInstaller
                        = ToolPackageFactory.CreateToolPackageStoresAndInstaller(
                            additionalRestoreArguments: parseResult.OptionValuesToBeForwarded(ToolUpdateCommandParser.GetCommand()));
                _toolPackageInstaller = toolPackageStoresAndInstaller.installer;
            }
            else
            {
                _toolPackageInstaller = toolPackageInstaller;
            }

            _toolManifestFinder = toolManifestFinder ??
                                  new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
            _toolManifestEditor = toolManifestEditor ?? new ToolManifestEditor();
            _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();
            _toolLocalPackageInstaller = new ToolInstallLocalInstaller(parseResult, toolPackageInstaller);
            _toolInstallLocalCommand = new Lazy<ToolInstallLocalCommand>(
                () => new ToolInstallLocalCommand(
                    parseResult,
                    _toolPackageInstaller,
                    _toolManifestFinder,
                    _toolManifestEditor,
                    _localToolsResolverCache,
                    _reporter));
        }

        public override int Execute()
        {
            (FilePath? manifestFileOptional, string warningMessage) = 
                _toolManifestFinder.ExplicitManifestOrFindManifestContainPackageId(_explicitManifestFile, _packageId);

            var manifestFile = manifestFileOptional ?? _toolManifestFinder.FindFirst();

            var toolDownloadedPackage = _toolLocalPackageInstaller.Install(manifestFile);
            var existingPackageWithPackageId =
                _toolManifestFinder
                    .Find(manifestFile)
                    .Where(p => p.PackageId.Equals(_packageId));

            if (!existingPackageWithPackageId.Any())
            {
                return _toolInstallLocalCommand.Value.Install(manifestFile);
            }

            var existingPackage = existingPackageWithPackageId.Single();
            if (existingPackage.Version > toolDownloadedPackage.Version)
            {
                throw new GracefulException(new[]
                    {
                        string.Format(
                            LocalizableStrings.UpdateLocaToolToLowerVersion,
                            toolDownloadedPackage.Version.ToNormalizedString(),
                            existingPackage.Version.ToNormalizedString(),
                            manifestFile.Value)
                    },
                    isUserError: false);
            }

            if (existingPackage.Version != toolDownloadedPackage.Version)
            {
                _toolManifestEditor.Edit(
                    manifestFile,
                    _packageId,
                    toolDownloadedPackage.Version,
                    toolDownloadedPackage.Commands.Select(c => c.Name).ToArray());
            }

            _localToolsResolverCache.SaveToolPackage(
                toolDownloadedPackage,
                _toolLocalPackageInstaller.TargetFrameworkToInstall);

            if (warningMessage != null)
            {
                _reporter.WriteLine(warningMessage.Yellow());
            }

            if (existingPackage.Version == toolDownloadedPackage.Version)
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.UpdateLocaToolSucceededVersionNoChange,
                        toolDownloadedPackage.Id,
                        existingPackage.Version.ToNormalizedString(),
                        manifestFile.Value));
            }
            else
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.UpdateLocalToolSucceeded,
                        toolDownloadedPackage.Id,
                        existingPackage.Version.ToNormalizedString(),
                        toolDownloadedPackage.Version.ToNormalizedString(),
                        manifestFile.Value).Green());
            }

            return 0;
        }
    }
}
