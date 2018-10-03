// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;
using Command = Microsoft.DotNet.Cli.Utils.Command;

namespace Microsoft.DotNet.Tools.Tool.Restore
{
    internal class ToolRestoreCommand : CommandBase
    {
        private readonly string _configFilePath;
        private readonly IReporter _errorReporter;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly IToolManifestFinder _toolManifestFinder;
        private readonly DirectoryPath _nugetGlobalPackagesFolder;
        private readonly AppliedOption _options;
        private readonly IReporter _reporter;
        private readonly string[] _sources;
        private readonly IToolPackageInstaller _toolPackageInstaller;
        private readonly string _verbosity;
        private const int LocalToolResolverCacheVersion = 1;

        public ToolRestoreCommand(
            AppliedOption appliedCommand,
            ParseResult result,
            IToolPackageInstaller toolPackageInstaller = null,
            IToolManifestFinder toolManifestFinder = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            DirectoryPath? nugetGlobalPackagesFolder = null,
            IReporter reporter = null)
            : base(result)
        {
            _options = appliedCommand ?? throw new ArgumentNullException(nameof(appliedCommand));

            if (toolPackageInstaller == null)
            {
                (IToolPackageStore,
                    IToolPackageStoreQuery,
                    IToolPackageInstaller installer) toolPackageStoresAndInstaller
                        = ToolPackageFactory.CreateToolPackageStoresAndInstaller(
                            additionalRestoreArguments: appliedCommand.OptionValuesToBeForwarded());
                _toolPackageInstaller = toolPackageStoresAndInstaller.installer;
            }
            else
            {
                _toolPackageInstaller = toolPackageInstaller;
            }

            _toolManifestFinder
                = toolManifestFinder
                  ?? new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));

            _localToolsResolverCache = localToolsResolverCache ??
                                       new LocalToolsResolverCache(
                                           new FileSystemWrapper(),
                                           new DirectoryPath(
                                               Path.Combine(CliFolderPathCalculator.ToolsResolverCachePath)),
                                           LocalToolResolverCacheVersion);

            _nugetGlobalPackagesFolder =
                nugetGlobalPackagesFolder ?? new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation());
            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;

            _configFilePath = appliedCommand.ValueOrDefault<string>("configfile");
            _sources = appliedCommand.ValueOrDefault<string[]>("add-source");
            _verbosity = appliedCommand.SingleArgumentOrDefault("verbosity");
        }

        public override int Execute()
        {
            FilePath? customManifestFileLocation = GetCustomManifestFileLocation();

            FilePath? configFile = null;
            if (_configFilePath != null) configFile = new FilePath(_configFilePath);

            IReadOnlyCollection<ToolManifestPackage> packagesFromManifest;
            try
            {
                packagesFromManifest = _toolManifestFinder.Find(customManifestFileLocation);
            }
            catch (ToolManifestCannotBeFoundException e)
            {
                _reporter.WriteLine(e.Message.Yellow());
                return 0;
            }

            Dictionary<RestoredCommandIdentifier, RestoredCommand> dictionary =
                new Dictionary<RestoredCommandIdentifier, RestoredCommand>();

            Dictionary<PackageId, ToolPackageException> toolPackageExceptions =
                new Dictionary<PackageId, ToolPackageException>();

            List<string> errorMessages = new List<string>();
            List<string> successMessages = new List<string>();

            foreach (var package in packagesFromManifest)
            {
                string targetFramework = BundledTargetFramework.GetTargetFrameworkMoniker();

                if (PackageHasBeenRestored(package, targetFramework))
                {
                    successMessages.Add(string.Format(
                        LocalizableStrings.RestoreSuccessful, package.PackageId,
                        package.Version.ToNormalizedString(), string.Join(", ", package.CommandNames)));
                    continue;
                }

                try
                {
                    IToolPackage toolPackage =
                        _toolPackageInstaller.InstallPackageToExternalManagedLocation(
                            new PackageLocation(
                                nugetConfig: configFile,
                                additionalFeeds: _sources),
                            package.PackageId, ToVersionRangeWithOnlyOneVersion(package.Version), targetFramework,
                            verbosity: _verbosity);

                    if (!ManifestCommandMatchesActualInPackage(package.CommandNames, toolPackage.Commands))
                    {
                        errorMessages.Add(
                            string.Format(LocalizableStrings.CommandsMismatch,
                                package.PackageId,
                                JoinBySpaceWithQuote(toolPackage.Commands.Select(c => c.Name.ToString())),
                                JoinBySpaceWithQuote(package.CommandNames.Select(c => c.Value.ToString()))));
                    }

                    foreach (RestoredCommand command in toolPackage.Commands)
                    {
                        dictionary.Add(
                            new RestoredCommandIdentifier(
                                toolPackage.Id,
                                toolPackage.Version,
                                NuGetFramework.Parse(targetFramework),
                                "any",
                                command.Name),
                            command);
                    }

                    successMessages.Add(string.Format(
                        LocalizableStrings.RestoreSuccessful, package.PackageId,
                        package.Version.ToNormalizedString(), string.Join(" ", package.CommandNames)));
                }
                catch (ToolPackageException e)
                {
                    toolPackageExceptions.Add(package.PackageId, e);
                }
            }

            EnsureNoCommandNameCollision(dictionary);

            _localToolsResolverCache.Save(dictionary, _nugetGlobalPackagesFolder);

            if (toolPackageExceptions.Any() || errorMessages.Any())
            {
                var partialOrTotalFailed = dictionary.Count() > 0
                    ? LocalizableStrings.RestorePartiallySuccessful
                    : LocalizableStrings.RestoreFailed;

                _errorReporter.WriteLine(partialOrTotalFailed +
                                         Environment.NewLine +
                                         string.Join(
                                             Environment.NewLine,
                                             CreateErrorMessage(toolPackageExceptions).Concat(errorMessages)));

                return 1;
            }

            _reporter.WriteLine(LocalizableStrings.LocalToolsRestoreWasSuccessful.Green());
            _reporter.WriteLine(string.Join(Environment.NewLine, successMessages).Green());

            return 0;
        }

        private static IEnumerable<string> CreateErrorMessage(
            Dictionary<PackageId, ToolPackageException> toolPackageExceptions)
        {
            return toolPackageExceptions.Select(p =>
                string.Format(LocalizableStrings.PackageFailedToRestore,
                    p.Key.ToString(), p.Value.ToString()));
        }

        private static bool ManifestCommandMatchesActualInPackage(
            ToolCommandName[] commandsFromManifest,
            IReadOnlyList<RestoredCommand> toolPackageCommands)
        {
            ToolCommandName[] commandsFromPackage = toolPackageCommands.Select(t => t.Name).ToArray();
            foreach (var command in commandsFromManifest)
            {
                if (!commandsFromPackage.Contains(command))
                {
                    return false;
                }
            }

            foreach (var command in commandsFromPackage)
            {
                if (!commandsFromManifest.Contains(command))
                {
                    return false;
                }
            }

            return true;
        }

        private bool PackageHasBeenRestored(
            ToolManifestPackage package,
            string targetFramework)
        {
            var sampleRestoredCommandIdentifierOfThePackage = new RestoredCommandIdentifier(
                package.PackageId,
                package.Version,
                NuGetFramework.Parse(targetFramework),
                "any",
                package.CommandNames.First());

            if (_localToolsResolverCache.TryLoad(
                sampleRestoredCommandIdentifierOfThePackage,
                _nugetGlobalPackagesFolder,
                out _))
            {
                return true;
            }

            return false;
        }

        private FilePath? GetCustomManifestFileLocation()
        {
            string customFile = _options.ValueOrDefault<string>("tool-manifest");
            FilePath? customManifestFileLocation;
            if (customFile != null)
            {
                customManifestFileLocation = new FilePath(customFile);
            }
            else
            {
                customManifestFileLocation = null;
            }

            return customManifestFileLocation;
        }

        private void EnsureNoCommandNameCollision(Dictionary<RestoredCommandIdentifier, RestoredCommand> dictionary)
        {
            string[] errors = dictionary
                .Select(pair => (PackageId: pair.Key.PackageId, CommandName: pair.Key.CommandName))
                .GroupBy(packageIdAndCommandName => packageIdAndCommandName.CommandName)
                .Where(grouped => grouped.Count() > 1)
                .Select(nonUniquePackageIdAndCommandNames =>
                    string.Format(LocalizableStrings.PackagesCommandNameCollision,
                        JoinBySpaceWithQuote(nonUniquePackageIdAndCommandNames.Select(a => a.PackageId.ToString())),
                        JoinBySpaceWithQuote(nonUniquePackageIdAndCommandNames.Select(a => a.CommandName.ToString()))))
                .ToArray();

            if (errors.Any())
            {
                throw new ToolPackageException(string.Join(Environment.NewLine, errors));
            }
        }

        private static string JoinBySpaceWithQuote(IEnumerable<object> objects)
        {
            return string.Join(" ", objects.Select(o => $"\"{o.ToString()}\""));
        }

        private static VersionRange ToVersionRangeWithOnlyOneVersion(NuGetVersion version)
        {
            return new VersionRange(
                version,
                includeMinVersion: true,
                maxVersion: version,
                includeMaxVersion: true);
        }
    }
}
