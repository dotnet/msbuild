// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.List
{
    internal delegate IToolPackageStoreQuery CreateToolPackageStore(DirectoryPath? nonGlobalLocation = null);

    internal class ToolListGlobalOrToolPathCommand : CommandBase
    {
        public const string CommandDelimiter = ", ";
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;
        private CreateToolPackageStore _createToolPackageStore;

        public ToolListGlobalOrToolPathCommand(
            ParseResult result,
            CreateToolPackageStore createToolPackageStore = null,
            IReporter reporter = null)
            : base(result)
        {
            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;
            _createToolPackageStore = createToolPackageStore ?? ToolPackageFactory.CreateToolPackageStoreQuery;
        }

        public override int Execute()
        {
            var toolPathOption = _parseResult.GetValueForOption(ToolListCommandParser.ToolPathOption);

            DirectoryPath? toolPath = null;
            if (!string.IsNullOrWhiteSpace(toolPathOption))
            {
                if (!Directory.Exists(toolPathOption))
                {
                    throw new GracefulException(
                        string.Format(
                            LocalizableStrings.InvalidToolPathOption,
                            toolPathOption));
                }

                toolPath = new DirectoryPath(toolPathOption);
            }

            var table = new PrintableTable<IToolPackage>();

            table.AddColumn(
                LocalizableStrings.PackageIdColumn,
                p => p.Id.ToString());
            table.AddColumn(
                LocalizableStrings.VersionColumn,
                p => p.Version.ToNormalizedString());
            table.AddColumn(
                LocalizableStrings.CommandsColumn,
                p => string.Join(CommandDelimiter, p.Commands.Select(c => c.Name)));

            table.PrintRows(GetPackages(toolPath), l => _reporter.WriteLine(l));
            return 0;
        }

        private IEnumerable<IToolPackage> GetPackages(DirectoryPath? toolPath)
        {
            return _createToolPackageStore(toolPath).EnumeratePackages()
                .Where(PackageHasCommands)
                .OrderBy(p => p.Id)
                .ToArray();
        }

        private bool PackageHasCommands(IToolPackage package)
        {
            try
            {
                // Attempt to read the commands collection
                // If it fails, print a warning and treat as no commands
                return package.Commands.Count >= 0;
            }
            catch (Exception ex) when (ex is ToolConfigurationException)
            {
                _errorReporter.WriteLine(
                    string.Format(
                        LocalizableStrings.InvalidPackageWarning,
                        package.Id,
                        ex.Message).Yellow());
                return false;
            }
        }
    }
}
