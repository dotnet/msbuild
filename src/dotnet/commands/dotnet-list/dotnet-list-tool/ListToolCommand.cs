// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.List.Tool
{
    internal class ListToolCommand : CommandBase
    {
        private const string CommandDelimiter = ", ";
        private readonly AppliedOption _options;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;

        public ListToolCommand(
            AppliedOption options,
            ParseResult result,
            IToolPackageStore toolPackageStore = null,
            IReporter reporter = null)
            : base(result)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _toolPackageStore = toolPackageStore ?? new ToolPackageStore(
                new DirectoryPath(new CliFolderPathCalculator().ToolsPackagePath));
            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;
        }

        public override int Execute()
        {
            if (!_options.ValueOrDefault<bool>("global"))
            {
                throw new GracefulException(LocalizableStrings.ListToolCommandOnlySupportsGlobal);
            }

            var table = new PrintableTable<IToolPackage>();

            table.AddColumn(
                LocalizableStrings.PackageIdColumn,
                p => p.PackageId);
            table.AddColumn(
                LocalizableStrings.VersionColumn,
                p => p.PackageVersion);
            table.AddColumn(
                LocalizableStrings.CommandsColumn,
                p => string.Join(CommandDelimiter, p.Commands.Select(c => c.Name)));

            table.PrintRows(GetPackages(), l => _reporter.WriteLine(l));
            return 0;
        }

        private IEnumerable<IToolPackage> GetPackages()
        {
            return _toolPackageStore.GetInstalledPackages()
                .Where(PackageHasCommands)
                .OrderBy(p => p.PackageId)
                .ToArray();
        }

        private bool PackageHasCommands(IToolPackage p)
        {
            try
            {
                // Attempt to read the commands collection
                // If it fails, print a warning and treat as no commands
                return p.Commands.Count >= 0;
            }
            catch (Exception ex) when (ex is ToolConfigurationException)
            {
                _errorReporter.WriteLine(
                    string.Format(
                        LocalizableStrings.InvalidPackageWarning,
                        p.PackageId,
                        ex.Message).Yellow());
                return false;
            }
        }
    }
}
