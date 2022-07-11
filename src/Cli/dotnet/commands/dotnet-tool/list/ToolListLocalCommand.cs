// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.List
{
    internal class ToolListLocalCommand : CommandBase
    {
        private readonly IToolManifestInspector _toolManifestInspector;
        private readonly IReporter _reporter;
        private const string CommandDelimiter = ", ";

        public ToolListLocalCommand(
            ParseResult parseResult,
            IToolManifestInspector toolManifestInspector = null,
            IReporter reporter = null)
            : base(parseResult)
        {
            _reporter = (reporter ?? Reporter.Output);

            _toolManifestInspector = toolManifestInspector ??
                                     new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
        }

        public override int Execute()
        {
            var table = new PrintableTable<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)>();

            table.AddColumn(
                LocalizableStrings.PackageIdColumn,
                p => p.toolManifestPackage.PackageId.ToString());
            table.AddColumn(
                LocalizableStrings.VersionColumn,
                p => p.toolManifestPackage.Version.ToNormalizedString());
            table.AddColumn(
                LocalizableStrings.CommandsColumn,
                p => string.Join(CommandDelimiter, p.toolManifestPackage.CommandNames.Select(c => c.Value)));
            table.AddColumn(
                LocalizableStrings.ManifestFileColumn,
                p => p.SourceManifest.Value);

            table.PrintRows(_toolManifestInspector.Inspect(), l => _reporter.WriteLine(l));
            return 0;
        }
    }
}
