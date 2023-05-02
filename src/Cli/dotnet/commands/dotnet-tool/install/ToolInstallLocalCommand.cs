// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Collections.Generic;
using NuGet.Packaging;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal class ToolInstallLocalCommand : CommandBase
    {
        private readonly IToolManifestFinder _toolManifestFinder;
        private readonly IToolManifestEditor _toolManifestEditor;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly ToolInstallLocalInstaller _toolLocalPackageInstaller;
        private readonly IReporter _reporter;

        private readonly string _explicitManifestFile;
        private readonly bool _createManifestIfNeeded;

        public ToolInstallLocalCommand(
            ParseResult parseResult,
            IToolPackageInstaller toolPackageInstaller = null,
            IToolManifestFinder toolManifestFinder = null,
            IToolManifestEditor toolManifestEditor = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            IReporter reporter = null
            )
            : base(parseResult)
        {
            _explicitManifestFile = parseResult.GetValue(ToolAppliedOption.ToolManifestOption);

            _createManifestIfNeeded = parseResult.GetValue(ToolInstallCommandParser.CreateManifestIfNeededOption);

            _reporter = (reporter ?? Reporter.Output);

            _toolManifestFinder = toolManifestFinder ??
                                  new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
            _toolManifestEditor = toolManifestEditor ?? new ToolManifestEditor();
            _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();
            _toolLocalPackageInstaller = new ToolInstallLocalInstaller(parseResult, toolPackageInstaller);
        }

        public override int Execute()
        {
            FilePath manifestFile = GetManifestFilePath();
            return Install(manifestFile);
        }

        public int Install(FilePath manifestFile)
        {
            IToolPackage toolDownloadedPackage =
                _toolLocalPackageInstaller.Install(manifestFile);

            _toolManifestEditor.Add(
                manifestFile,
                toolDownloadedPackage.Id,
                toolDownloadedPackage.Version,
                toolDownloadedPackage.Commands.Select(c => c.Name).ToArray());

            _localToolsResolverCache.SaveToolPackage(
                toolDownloadedPackage,
                _toolLocalPackageInstaller.TargetFrameworkToInstall);

            _reporter.WriteLine(
                string.Format(
                    LocalizableStrings.LocalToolInstallationSucceeded,
                    string.Join(", ", toolDownloadedPackage.Commands.Select(c => c.Name)),
                    toolDownloadedPackage.Id,
                    toolDownloadedPackage.Version.ToNormalizedString(),
                    manifestFile.Value).Green());

            return 0;
        }

        private FilePath GetManifestFilePath()
        {
            try
            {
                return string.IsNullOrWhiteSpace(_explicitManifestFile)
                    ? _toolManifestFinder.FindFirst(_createManifestIfNeeded)
                    : new FilePath(_explicitManifestFile);
            }
            catch (ToolManifestCannotBeFoundException e)
            {
                throw new GracefulException(new[]
                    {
                        e.Message,
                        LocalizableStrings.NoManifestGuide
                    },
                    verboseMessages: new[] {e.VerboseMessage},
                    isUserError: false);
            }
        }
    }
}
