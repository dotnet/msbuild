// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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
            var toolPathOption = _parseResult.GetValue(ToolListCommandParser.ToolPathOption);
            var packageIdArgument = _parseResult.GetValue(ToolListCommandParser.PackageIdArgument);

            PackageId? packageId = null;
            if (!string.IsNullOrWhiteSpace(packageIdArgument))
            {
                packageId = new PackageId(packageIdArgument);
            }

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

            var packageEnumerable = GetPackages(toolPath, packageId);
            table.PrintRows(packageEnumerable, l => _reporter.WriteLine(l));
            if (packageId.HasValue && !packageEnumerable.Any())
            {
                // return 1 if target package was not found
                return 1;
            }
            return 0;
        }

        private IEnumerable<IToolPackage> GetPackages(DirectoryPath? toolPath, PackageId? packageId)
        {
            return _createToolPackageStore(toolPath).EnumeratePackages()
                .Where((p) => PackageHasCommands(p) && PackageIdMatches(p, packageId))
                .OrderBy(p => p.Id)
                .ToArray();
        }

        internal static bool PackageIdMatches(IToolPackage package, PackageId? packageId)
        {
            return !packageId.HasValue || package.Id.Equals(packageId);
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
