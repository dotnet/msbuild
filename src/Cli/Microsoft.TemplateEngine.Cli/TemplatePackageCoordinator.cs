// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Versioning;
using static Microsoft.TemplateEngine.Cli.NuGet.NugetApiManager;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The class is responsible for template package manipulation flows: install template packages (-i, --install), check for update (--update-check), apply updates (--update-apply), uninstall template packages (-u, --uninstall).
    /// </summary>
    internal class TemplatePackageCoordinator
    {
        private const string SourceFeedKey = "NuGetSource";
        private const string NugetOrgFeed = "https://api.nuget.org/v3/index.json";

        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly TemplateConstraintManager _constraintsManager;
        private readonly HostSpecificDataLoader _hostSpecificDataLoader;
        private readonly TemplatePackageDisplay _templatePackageDisplay;

        internal TemplatePackageCoordinator(
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager)
        {
            _engineEnvironmentSettings = environmentSettings ?? throw new ArgumentNullException(nameof(environmentSettings));
            _templatePackageManager = templatePackageManager ?? throw new ArgumentNullException(nameof(templatePackageManager));
            _constraintsManager = new TemplateConstraintManager(_engineEnvironmentSettings);
            _hostSpecificDataLoader = new HostSpecificDataLoader(_engineEnvironmentSettings);
            _templatePackageDisplay = new TemplatePackageDisplay(Reporter.Output, Reporter.Error);
        }

        /// <summary>
        /// Checks if there is an update for the package containing the template to execute.
        /// </summary>
        /// <param name="args">template command arguments.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Task for checking the update or null when check for update is not possible.</returns>
        internal async Task<CheckUpdateResult?> CheckUpdateForTemplate(TemplateCommandArgs args, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //if update check is disabled - do nothing
            if (args.NoUpdateCheck)
            {
                return null;
            }

            ITemplatePackage templatePackage;
            try
            {
                templatePackage = await _templatePackageManager.GetTemplatePackageAsync(args.Template, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Error_PackageForTemplateNotFound, args.Template.Identity);
                return null;
            }

            if (!(templatePackage is IManagedTemplatePackage managedTemplatePackage))
            {
                //update is not supported - built-in or optional workload source
                return null;
            }
            InitializeNuGetCredentialService(interactive: false);
            return (await managedTemplatePackage.ManagedProvider.GetLatestVersionsAsync(new[] { managedTemplatePackage }, cancellationToken).ConfigureAwait(false)).Single();
        }

        internal async Task<(string Id, string Version, string Provider)> ValidateBuiltInPackageAvailabilityAsync(
            ITemplateInfo template,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ITemplatePackage templatePackage;
            try
            {
                templatePackage = await _templatePackageManager.GetTemplatePackageAsync(template, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Error_PackageForTemplateNotFound, template.Identity);
                return default;
            }

            if (!(templatePackage is IManagedTemplatePackage managedTemplatePackage))
            {
                //update is not supported - built-in or optional workload source
                return default;
            }

            IReadOnlyList<ITemplatePackage> templatePackages = await _templatePackageManager.GetTemplatePackagesAsync(force: false, cancellationToken).ConfigureAwait(false);

            IEnumerable<(string Id, string Version, string Provider)> unmanagedTemplatePackages = templatePackages
                .Where(tp => tp is not IManagedTemplatePackage)
                .Select(tp => new
                {
                    Info = NuGetUtils.GetNuGetPackageInfo(_engineEnvironmentSettings, tp.MountPointUri),
                    Package = tp
                })
                .Where(i => i.Info != default)
                .Select(i => (i.Info.Id, i.Info.Version, i.Package.Provider.Factory.DisplayName));

            var matchingTemplatePackage = unmanagedTemplatePackages.FirstOrDefault(package => string.Equals(managedTemplatePackage.Identifier, package.Id, StringComparison.OrdinalIgnoreCase));
            if (matchingTemplatePackage == default)
            {
                return default;
            }

            NuGetVersion? managedPackageVersion;
            NuGetVersion? unmanagedPackageVersion;

            if (NuGetVersion.TryParse(managedTemplatePackage.Version, out managedPackageVersion) && NuGetVersion.TryParse(matchingTemplatePackage.Version, out unmanagedPackageVersion))
            {
                if (unmanagedPackageVersion >= managedPackageVersion)
                {
                    return matchingTemplatePackage;
                }
            }
            return default;
        }

        internal void DisplayUpdateCheckResult(CheckUpdateResult versionCheckResult, ICommandArgs args)
        {
            _ = versionCheckResult ?? throw new ArgumentNullException(nameof(versionCheckResult));

            if (versionCheckResult.Success)
            {
                if (!versionCheckResult.IsLatestVersion)
                {
                    string displayString = $"{versionCheckResult.TemplatePackage?.Identifier}::{versionCheckResult.TemplatePackage?.Version}";         // the package::version currently installed
                    Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateAvailable, displayString);

                    Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateSingleCommandHeader);
                    Reporter.Output.WriteCommand(
                        Example
                            .For<NewCommand>(args.ParseResult)
                            .WithSubcommand<InstallCommand>()
                            .WithArgument(InstallCommand.NameArgument, $"{versionCheckResult.TemplatePackage?.Identifier}::{versionCheckResult.LatestVersion}"));
                    Reporter.Output.WriteLine();
                }
            }
            else
            {
                HandleUpdateCheckErrors(versionCheckResult, ignoreLocalPackageNotFound: true);
                Reporter.Error.WriteLine();
            }
        }

        internal void DisplayBuiltInPackagesCheckResult(string packageId, string version, string provider, ICommandArgs args)
        {
            Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_BuiltInCheck_Info_BuiltInPackageAvailable, $"{packageId}::{version}", provider);
            Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_BuiltInCheck_Info_UninstallPackage);
            Reporter.Output.WriteCommand(
                Example
                 .For<NewCommand>(args.ParseResult)
                 .WithSubcommand<UninstallCommand>()
                 .WithArgument(UninstallCommand.NameArgument, packageId));
        }

        /// <summary>
        /// Install the template package(s) flow (--install, -i).
        /// </summary>
        internal async Task<NewCommandStatus> EnterInstallFlowAsync(InstallCommandArgs args, CancellationToken cancellationToken)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            _ = args.TemplatePackages ?? throw new ArgumentNullException(nameof(args.TemplatePackages));
            if (!args.TemplatePackages.Any())
            {
                throw new ArgumentException($"{nameof(args.TemplatePackages)} should have at least one item to continue.", nameof(args.TemplatePackages));
            }
            cancellationToken.ThrowIfCancellationRequested();
            InitializeNuGetCredentialService(args.Interactive);

            NewCommandStatus resultStatus = NewCommandStatus.Success;
            TelemetryEventEntry.TrackEvent(TelemetryConstants.InstallEvent, new Dictionary<string, string?> { { TelemetryConstants.ToInstallCount, args.TemplatePackages.Count.ToString() } });

            var details = new Dictionary<string, string>();
            if (args.AdditionalSources?.Count > 0)
            {
                details[InstallerConstants.NuGetSourcesKey] = string.Join(InstallerConstants.NuGetSourcesSeparator.ToString(), args.AdditionalSources);
            }
            if (args.Interactive)
            {
                details[InstallerConstants.InteractiveModeKey] = "true";
            }

            // In future we might want give user ability to pick IManagerSourceProvider by Name or GUID
            var managedSourceProvider = _templatePackageManager.GetBuiltInManagedProvider(InstallationScope.Global);
            List<InstallRequest> installRequests = new List<InstallRequest>();

            foreach (string installArg in args.TemplatePackages)
            {
                string[] splitByColons = installArg.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                string identifier = splitByColons[0];
                string? version = splitByColons.Length > 1 ? splitByColons[1] : null;
                foreach (string expandedIdentifier in InstallRequestPathResolution.ExpandMaskedPath(identifier, _engineEnvironmentSettings))
                {
                    installRequests.Add(new InstallRequest(expandedIdentifier, version, details: details, force: args.Force));
                }
            }

            if (!installRequests.Any())
            {
                Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_Error_FoundNoPackagesToInstall);
                return NewCommandStatus.NotFound;
            }

            //validate if installation requests have unique identifier
            HashSet<string> identifiers = new HashSet<string>();
            foreach (InstallRequest installRequest in installRequests)
            {
                if (identifiers.Add(installRequest.PackageIdentifier))
                {
                    continue;
                }
                Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_Error_SameInstallRequests, installRequest.PackageIdentifier);
                return NewCommandStatus.InstallFailed;
            }

            Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_Info_PackagesToBeInstalled);
            foreach (InstallRequest installRequest in installRequests)
            {
                Reporter.Output.WriteLine(installRequest.DisplayName.Indent());
            }
            Reporter.Output.WriteLine();

            bool validated = await ValidateInstallationRequestsAsync(args, installRequests, cancellationToken).ConfigureAwait(false);
            if (!validated)
            {
                return NewCommandStatus.InstallFailed;
            }

            IReadOnlyList<InstallResult> installResults = await managedSourceProvider.InstallAsync(installRequests, cancellationToken).ConfigureAwait(false);
            foreach (InstallResult result in installResults)
            {
                await _templatePackageDisplay.DisplayInstallResultAsync(
                    result.InstallRequest.DisplayName,
                    result,
                    args.ParseResult,
                    args.Force,
                    _templatePackageManager,
                    _engineEnvironmentSettings,
                    _constraintsManager,
                    cancellationToken).ConfigureAwait(false);
                if (!result.Success)
                {
                    resultStatus = result.Error == InstallerErrorCode.PackageNotFound ? NewCommandStatus.NotFound : NewCommandStatus.InstallFailed;
                }
            }
            return resultStatus;
        }

        /// <summary>
        /// Update the template package(s) flow (--update-check and --update-apply).
        /// </summary>
        internal async Task<NewCommandStatus> EnterUpdateFlowAsync(UpdateCommandArgs commandArgs, CancellationToken cancellationToken)
        {
            _ = commandArgs ?? throw new ArgumentNullException(nameof(commandArgs));
            cancellationToken.ThrowIfCancellationRequested();
            InitializeNuGetCredentialService(commandArgs.Interactive);

            bool applyUpdates = !commandArgs.CheckOnly;
            bool allTemplatesUpToDate = true;
            NewCommandStatus success = NewCommandStatus.Success;
            var managedTemplatePackages = await _templatePackageManager.GetManagedTemplatePackagesAsync(false, cancellationToken).ConfigureAwait(false);

            foreach (var packagesGrouping in managedTemplatePackages.GroupBy(package => package.ManagedProvider))
            {
                var provider = packagesGrouping.Key;
                IReadOnlyList<CheckUpdateResult> checkUpdateResults = await provider.GetLatestVersionsAsync(packagesGrouping, cancellationToken).ConfigureAwait(false);
                _templatePackageDisplay.DisplayUpdateCheckResults(_engineEnvironmentSettings, checkUpdateResults, commandArgs, showUpdates: !applyUpdates);
                if (checkUpdateResults.Any(result => !result.Success))
                {
                    success = NewCommandStatus.InstallFailed;
                }
                allTemplatesUpToDate = checkUpdateResults.All(result => result.Success && result.IsLatestVersion);

                if (applyUpdates)
                {
                    IEnumerable<CheckUpdateResult> updatesToApply = checkUpdateResults.Where(update => update.Success && !update.IsLatestVersion);
                    if (!updatesToApply.Any())
                    {
                        continue;
                    }
                    if (updatesToApply.Any(update => update.TemplatePackage is null || update.LatestVersion is null))
                    {
                        throw new InvalidOperationException($"Unexpected result received from {nameof(provider.GetLatestVersionsAsync)} method: returned result where {nameof(CheckUpdateResult.TemplatePackage)} is null, or {nameof(CheckUpdateResult.LatestVersion)} is null.");
                    }

                    Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_PackagesToBeUpdated);
                    foreach (CheckUpdateResult update in updatesToApply)
                    {
                        Reporter.Output.WriteLine($"{update.TemplatePackage!.Identifier}::{update.LatestVersion}".Indent());
                    }
                    Reporter.Output.WriteLine();

                    IReadOnlyList<UpdateResult> updateResults = await provider.UpdateAsync(updatesToApply.Select(update => new UpdateRequest(update.TemplatePackage!, update.LatestVersion!)), cancellationToken).ConfigureAwait(false);
                    foreach (var updateResult in updateResults)
                    {
                        if (!updateResult.Success)
                        {
                            success = NewCommandStatus.InstallFailed;
                        }

                        await _templatePackageDisplay.DisplayInstallResultAsync(
                           updateResult.UpdateRequest.TemplatePackage.DisplayName,
                           updateResult,
                           commandArgs.ParseResult,
                           // force is not supported by update flow
                           force: false,
                           _templatePackageManager,
                           _engineEnvironmentSettings,
                           _constraintsManager,
                           cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (allTemplatesUpToDate)
            {
                Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_AllPackagesAreUpToDate);
            }

            return success;
        }

        /// <summary>
        /// Uninstall the template package(s) flow (--uninstall, -u).
        /// </summary>
        internal async Task<NewCommandStatus> EnterUninstallFlowAsync(UninstallCommandArgs args, CancellationToken cancellationToken)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            cancellationToken.ThrowIfCancellationRequested();

            NewCommandStatus result = NewCommandStatus.Success;
            if (args.TemplatePackages == null || args.TemplatePackages.Count <= 0)
            {
                //display all installed template packages
                await _templatePackageDisplay.DisplayInstalledTemplatePackagesAsync(_templatePackageManager, args, cancellationToken).ConfigureAwait(false);
                return result;
            }

            Dictionary<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>> sourcesToUninstall;
            (result, sourcesToUninstall) = await DetermineSourcesToUninstallAsync(args, cancellationToken).ConfigureAwait(false);

            foreach (KeyValuePair<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>> providerSourcesToUninstall in sourcesToUninstall)
            {
                IReadOnlyList<UninstallResult> uninstallResults = await providerSourcesToUninstall.Key.UninstallAsync(providerSourcesToUninstall.Value, cancellationToken).ConfigureAwait(false);
                foreach (UninstallResult uninstallResult in uninstallResults)
                {
                    if (uninstallResult.Success)
                    {
                        Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Info_Success, uninstallResult.TemplatePackage.DisplayName);
                    }
                    else
                    {
                        Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Error_GenericError, uninstallResult.TemplatePackage.DisplayName, uninstallResult.ErrorMessage);
                        result = NewCommandStatus.InstallFailed;
                    }
                }
            }
            //rebuild cache after uninstall to remove deleted templates.
            await _templatePackageManager.RebuildTemplateCacheAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Searches and displays a package metadata.
        /// </summary>
        internal async Task<NewCommandStatus> DisplayTemplatePackageMetadata(
            string packageIdentity,
            string? packageVersion,
            bool interactiveAuth,
            IReadOnlyList<string>? additionalSources,
            NugetApiManager nugetApiManager,
            CancellationToken cancellationToken = default)
        {
            NugetPackageMetadata? nuGetPackageMetadata;
            IEnumerable<ITemplateInfo>? packageTemplates;
            IManagedTemplatePackage? localPackage;

            InitializeNuGetCredentialService(interactiveAuth);

            try
            {
                (localPackage, packageTemplates) = await _templatePackageManager
                    .GetManagedTemplatePackageAsync(packageIdentity, packageVersion, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                localPackage = null;
                packageTemplates = null;
            }

            // The package was found locally
            if (localPackage != null && packageTemplates != null)
            {
                string? packageSource = string.Empty;
                PackageSource? sourceFeed = null;
                if (localPackage.GetDetails().TryGetValue(SourceFeedKey, out packageSource))
                {
                    sourceFeed = new PackageSource(packageSource);
                }

                nuGetPackageMetadata = await nugetApiManager.GetPackageMetadataAsync(
                    packageIdentity,
                    packageVersion,
                    sourceFeed,
                    cancellationToken).ConfigureAwait(false);

                if (nuGetPackageMetadata == null)
                {
                    DisplayLocalPackageMetadata(localPackage, Reporter.Output);

                    var templatesToDisplay = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(packageTemplates, null, null, _engineEnvironmentSettings.Environment);
                    DisplayPackageTemplateList(templatesToDisplay, Reporter.Output);
                    return NewCommandStatus.Success;
                }
            }
            else
            {
                IEnumerable<PackageSource> packageSources = LoadNuGetSources(additionalSources, true);

                nuGetPackageMetadata = await GetPackageMetadataFromMultipleFeedsAsync(packageSources, nugetApiManager, packageIdentity, packageVersion, cancellationToken).ConfigureAwait(false);
                if (nuGetPackageMetadata != null && nuGetPackageMetadata.Source.Source.Equals(NugetOrgFeed))
                {
                    packageTemplates = await CliTemplateSearchCoordinator.SearchForPackageTemplatesAsync(
                        _engineEnvironmentSettings,
                        packageIdentity,
                        packageVersion,
                        cancellationToken).ConfigureAwait(false);
                }

            }

            if (nuGetPackageMetadata != null)
            {
                DisplayNuGetPackageMetadata(nuGetPackageMetadata, Reporter.Output);
                if (packageTemplates != null && packageTemplates.Any())
                {
                    var templatesToDisplay = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(packageTemplates, null, null, _engineEnvironmentSettings.Environment);
                    DisplayPackageTemplateList(templatesToDisplay, Reporter.Output);
                }
                return NewCommandStatus.Success;
            }

            Reporter.Output.WriteLine(
                LocalizableStrings.Generic_Info_NoMatchingTemplatePackage.Bold().Red(),
                $"{packageIdentity}{(string.IsNullOrWhiteSpace(packageVersion) ? string.Empty : $"::{packageVersion}")}");

            return NewCommandStatus.NotFound;
        }

        internal void DisplayNuGetPackageMetadata(NugetPackageMetadata packageMetadata, IReporter reporter)
        {
            reporter.WriteLine($"{packageMetadata.Identity.Id}");
            WriteIfNotNull(LocalizableStrings.DetailsCommand_Property_Version, packageMetadata.PackageVersion.ToString(), reporter, 1);
            if (packageMetadata.PrefixReserved != null && packageMetadata.Source.Source.Equals(NugetOrgFeed))
            {
                WriteIfNotNull(LocalizableStrings.DetailsCommand_Property_PrefixReserved, packageMetadata.PrefixReserved.ToString(), reporter, 1);
            }
            WriteIfNotNull(LocalizableStrings.DetailsCommand_Property_Description, packageMetadata.Description, reporter, 1);

            string sourceFeed = packageMetadata.Source.Source == packageMetadata.Source.Name ? packageMetadata.Source.Source : $"{packageMetadata.Source.Name} [{packageMetadata.Source.Source}]";
            reporter.WriteLine($"{LocalizableStrings.DetailsCommand_Property_SourceFeed}: {sourceFeed}".Indent(1));

            if (!string.IsNullOrEmpty(packageMetadata.Authors))
            {
                reporter.WriteLine($"{LocalizableStrings.DetailsCommand_Property_Authors}:".Indent(1));

                var packageAuthors = packageMetadata.Authors.Split(",");
                foreach (var author in packageAuthors)
                {
                    reporter.WriteLine(author.Trim().Indent(2));
                }
            }

            if (!string.IsNullOrEmpty(packageMetadata.Owners))
            {
                reporter.WriteLine($"{LocalizableStrings.DetailsCommand_Property_Owners}:".Indent(1));

                var packageOwners = packageMetadata.Owners.Split(",");
                foreach (var owner in packageOwners)
                {
                    reporter.WriteLine(AnsiExtensions.Url($"https://nuget.org/profiles/{owner.Trim()}", owner).Indent(2));
                }
            }

            reporter.WriteLine($"{LocalizableStrings.DetailsCommand_Property_LicenseMetadata}:".Indent(1));
            WriteIfNotNull(LocalizableStrings.DetailsCommand_Property_License, packageMetadata.License, reporter, 2);

            if (!string.IsNullOrEmpty(packageMetadata.LicenseExpression))
            {
                var licenseExpressionUrl = "https://licenses.nuget.org/" + packageMetadata.LicenseExpression;
                reporter.WriteLine(
                    $"{LocalizableStrings.DetailsCommand_Property_LicenseExpression}: ".Indent(1) +
                    $"{AnsiExtensions.Url(licenseExpressionUrl, packageMetadata.LicenseExpression)}");
            }

            var licenseUrl = packageMetadata.LicenseUrl?.ToString();
            if (!string.IsNullOrEmpty(licenseUrl))
            {
                reporter.WriteLine(
                    $"{LocalizableStrings.DetailsCommand_Property_LicenseUrl}: ".Indent(2) +
                    $"{AnsiExtensions.Url(licenseUrl, licenseUrl)}");
            }

            var projectUrl = packageMetadata.ProjectUrl?.ToString();
            if (!string.IsNullOrEmpty(projectUrl))
            {
                reporter.WriteLine(
                    $"{LocalizableStrings.DetailsCommand_Property_RepoUrl}: {projectUrl}".Indent(2));
            }
        }

        internal void DisplayLocalPackageMetadata(IManagedTemplatePackage package, IReporter reporter)
        {
            reporter.WriteLine($"{package.Identifier}");

            var packageDetails = package.GetDetails();

            string? authors;
            packageDetails.TryGetValue("Author", out authors);
            if (!string.IsNullOrEmpty(authors))
            {
                reporter.WriteLine($"{LocalizableStrings.DetailsCommand_Property_Authors}:".Indent(1));

                var packageAuthors = authors.Split(",");
                foreach (var author in packageAuthors)
                {
                    reporter.WriteLine(author.Trim().Indent(2));
                }
            }

            string? nuGetSource;
            packageDetails.TryGetValue("NuGetSource", out nuGetSource);

            if (!string.IsNullOrEmpty(nuGetSource))
            {
                reporter.WriteLine(
                    $"{LocalizableStrings.DetailsCommand_Property_RepoUrl}: {nuGetSource}".Indent(1));
            }
        }

        internal void DisplayPackageTemplateList(IReadOnlyList<TemplateGroupTableRow> templatesToDisplay, IReporter reporter)
        {
            reporter.WriteLine($"{LocalizableStrings.DetailsCommand_Property_Templates}:".Indent(1));

            TabularOutput<TemplateGroupTableRow> formatter =
                TabularOutput.TabularOutput
                    .For(
                        new TabularOutputSettings(_engineEnvironmentSettings.Environment),
                        templatesToDisplay)
                    .DefineColumn(t => t.Name, LocalizableStrings.ColumnNameTemplateName, minWidth: 15, showAlways: true, shrinkIfNeeded: true)
                    .DefineColumn(t => t.ShortNames, LocalizableStrings.ColumnNameShortName, minWidth: 15, showAlways: true)
                    .DefineColumn(t => t.Type, LocalizableStrings.ColumnNameType, minWidth: 15, showAlways: true)
                    .DefineColumn(t => t.Classifications, LocalizableStrings.ColumnNameTags, minWidth: 15, showAlways: true, shrinkIfNeeded: true)
                    .DefineColumn(t => t.Languages, LocalizableStrings.ColumnNameLanguage, minWidth: 15, showAlways: true);

            reporter.WriteLine(formatter.Layout(2));
        }

        private static void InitializeNuGetCredentialService(bool interactive)
        {
            try
            {
                DefaultCredentialServiceUtility.SetupDefaultCredentialService(new CliNuGetLogger(), !interactive);
            }
            catch (Exception ex)
            {
                Reporter.Verbose.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Verbose_NuGetCredentialServiceError, ex.ToString());
            }
        }

        private void WriteIfNotNull(string metadataName, string? metadataEntry, IReporter reporter, int indent = 0)
        {
            if (!string.IsNullOrEmpty(metadataEntry))
            {
                reporter.WriteLine($"{metadataName}: {metadataEntry}".Indent(indent));
            }
        }

        private async Task<bool> ValidateInstallationRequestsAsync(InstallCommandArgs args, List<InstallRequest> installRequests, CancellationToken cancellationToken)
        {
            var templatePackages = await _templatePackageManager.GetTemplatePackagesAsync(force: false, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<(string Id, string Version)> unmanagedTemplatePackages = templatePackages
                .Where(tp => tp is not IManagedTemplatePackage)
                .Select(tp => NuGetUtils.GetNuGetPackageInfo(_engineEnvironmentSettings, tp.MountPointUri))
                .Where(i => i != default)
                .ToList();

            HashSet<(InstallRequest Request, (string Id, string Version) PackageInfo)> invalidTemplatePackages = new();

            foreach (var installRequest in installRequests)
            {
                var foundPackage = unmanagedTemplatePackages.FirstOrDefault(package => string.Equals(package.Id, installRequest.PackageIdentifier, StringComparison.OrdinalIgnoreCase));
                if (foundPackage != default)
                {
                    invalidTemplatePackages.Add((installRequest, foundPackage));
                }
            }

            if (invalidTemplatePackages.Any())
            {
                IReporter reporter = args.Force ? Reporter.Output : Reporter.Error;

                reporter.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_Info_OverrideNotice);
                reporter.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_Info_PackageIsAvailable);
                foreach (var request in invalidTemplatePackages)
                {
                    reporter.WriteLine($"{request.PackageInfo.Id}::{request.PackageInfo.Version}".Indent());
                }
                reporter.WriteLine();

                if (!args.Force)
                {
                    reporter.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_Info_UseForceToOverride, SharedOptions.ForceOption.Name);
                    reporter.WriteCommand(
                        Example
                            .For<InstallCommand>(args.ParseResult)
                            .WithArgument(InstallCommand.NameArgument, installRequests.Select(ir => ir.DisplayName).ToArray())
                            .WithOption(SharedOptions.ForceOption));
                    return false;
                }
            }
            return true;
        }

        private async Task<(NewCommandStatus, Dictionary<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>>)> DetermineSourcesToUninstallAsync(UninstallCommandArgs commandArgs, CancellationToken cancellationToken)
        {
            _ = commandArgs ?? throw new ArgumentNullException(nameof(commandArgs));
            _ = commandArgs.TemplatePackages ?? throw new ArgumentNullException(nameof(commandArgs.TemplatePackages));
            cancellationToken.ThrowIfCancellationRequested();

            NewCommandStatus result = NewCommandStatus.Success;
            IReadOnlyList<IManagedTemplatePackage> templatePackages = await _templatePackageManager.GetManagedTemplatePackagesAsync(false, cancellationToken).ConfigureAwait(false);

            var packagesToUninstall = new Dictionary<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>>();
            List<string> notFoundPackages = new List<string>();
            foreach (var requestedPackageIdentifier in commandArgs.TemplatePackages)
            {
                bool templatePackageIdentified = false;
                // First try to search for installed packages that have identical identifier as requested to be unistalled
                foreach (IManagedTemplatePackage templatePackage in templatePackages)
                {
                    if (templatePackage.Identifier.Equals(requestedPackageIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        templatePackageIdentified = true;
                        if (packagesToUninstall.TryGetValue(templatePackage.ManagedProvider, out List<IManagedTemplatePackage>? packages))
                        {
                            packages.Add(templatePackage);
                        }
                        else
                        {
                            packagesToUninstall[templatePackage.ManagedProvider] = new List<IManagedTemplatePackage>() { templatePackage };
                        }
                    }
                }

                if (!templatePackageIdentified)
                {
                    // If not found - try to expand path and search with expanded path for all local packages (folders and nugets)
                    foreach (string expandedIdentifier in InstallRequestPathResolution.ExpandMaskedPath(requestedPackageIdentifier, _engineEnvironmentSettings))
                    {
                        templatePackageIdentified = false;
                        foreach (IManagedTemplatePackage templatePackage in templatePackages.Where(pm => pm.IsLocalPackage))
                        {
                            if (templatePackage.Identifier.Equals(expandedIdentifier, StringComparison.OrdinalIgnoreCase))
                            {
                                templatePackageIdentified = true;
                                if (packagesToUninstall.TryGetValue(templatePackage.ManagedProvider, out List<IManagedTemplatePackage>? packages))
                                {
                                    packages.Add(templatePackage);
                                }
                                else
                                {
                                    packagesToUninstall[templatePackage.ManagedProvider] = new List<IManagedTemplatePackage>() { templatePackage };
                                }
                            }
                        }

                        if (!templatePackageIdentified)
                        {
                            notFoundPackages.Add(expandedIdentifier);
                        }
                    }
                }
            }

            foreach (string notFoundPackage in notFoundPackages)
            {
                result = NewCommandStatus.NotFound;
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatePackageCoordinator_Error_PackageNotFound,
                        notFoundPackage).Bold().Red());
                if (await IsTemplateShortNameAsync(notFoundPackage, cancellationToken).ConfigureAwait(false))
                {
                    var packages = await GetTemplatePackagesByShortNameAsync(notFoundPackage, cancellationToken).ConfigureAwait(false);
                    var managedPackages = packages.OfType<IManagedTemplatePackage>();
                    if (managedPackages.Any())
                    {
                        Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Error_TemplateIncludedToPackages, notFoundPackage);
                        foreach (IManagedTemplatePackage managedPackage in managedPackages)
                        {
                            IEnumerable<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(managedPackage, cancellationToken).ConfigureAwait(false);
                            var templateGroupsCount = templates.GroupBy(x => x.GroupIdentity, x => !string.IsNullOrEmpty(x.GroupIdentity), StringComparer.OrdinalIgnoreCase).Count();
                            Reporter.Error.WriteLine(
                                  string.Format(
                                      LocalizableStrings.TemplatePackageCoordinator_Error_PackageNameContainsTemplates,
                                      managedPackage.DisplayName,
                                      templateGroupsCount).Indent());
                        }
                        Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Error_UninstallCommandHeader);

                        if (string.IsNullOrWhiteSpace(managedPackages?.First().Identifier))
                        {
                            Reporter.Error.WriteCommand(
                                 Example
                                    .For<NewCommand>(commandArgs.ParseResult)
                                    .WithSubcommand<UninstallCommand>()
                                    .WithArgument(UninstallCommand.NameArgument));
                        }
                        else
                        {
                            Reporter.Error.WriteCommand(
                                 Example
                                    .For<NewCommand>(commandArgs.ParseResult)
                                    .WithSubcommand<UninstallCommand>()
                                    .WithArgument(UninstallCommand.NameArgument, managedPackages.First().Identifier));
                        }

                        //TODO:
                        //Reporter.Error.WriteLine($"To list the templates installed in a package, use dotnet new3 <new option> <package name>.");
                    }
                    else
                    {
                        Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Error_ListPackagesHeader);
                        Reporter.Error.WriteCommand(
                             Example
                                .For<NewCommand>(commandArgs.ParseResult)
                                .WithSubcommand<UninstallCommand>());
                    }
                }
                else
                {
                    Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Error_ListPackagesHeader);
                    Reporter.Error.WriteCommand(
                        Example
                           .For<NewCommand>(commandArgs.ParseResult)
                           .WithSubcommand<UninstallCommand>());
                }
                Reporter.Error.WriteLine();
            }

            return (result, packagesToUninstall);
        }

        private async Task<IEnumerable<ITemplatePackage>> GetTemplatePackagesByShortNameAsync(string sourceIdentifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourceIdentifier))
            {
                throw new ArgumentException(nameof(sourceIdentifier));
            }
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            var templatesWithMatchedShortName = templates.Where(template =>
            {
                return template.ShortNameList.Contains(sourceIdentifier, StringComparer.OrdinalIgnoreCase);
            });

            var templatePackages = await Task.WhenAll(
                templatesWithMatchedShortName.Select(
                    t => _templatePackageManager.GetTemplatePackageAsync(t, cancellationToken)))
                .ConfigureAwait(false);

            return templatePackages.Distinct();
        }

        private async Task<bool> IsTemplateShortNameAsync(string sourceIdentifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourceIdentifier))
            {
                throw new ArgumentException(nameof(sourceIdentifier));
            }
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            return templates.Any(template =>
            {
                return template.ShortNameList.Contains(sourceIdentifier, StringComparer.OrdinalIgnoreCase);
            });
        }

        private void DisplayUpdateCheckResults(IEnumerable<CheckUpdateResult> versionCheckResults, GlobalArgs args, bool showUpdates = true)
        {
            _ = versionCheckResults ?? throw new ArgumentNullException(nameof(versionCheckResults));

            //handle success
            if (versionCheckResults.Any(result => result.Success && !result.IsLatestVersion) && showUpdates)
            {
                Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateAvailablePackages);
                IEnumerable<(string Identifier, string? CurrentVersion, string? LatestVersion)> displayableResults = versionCheckResults
                    .Where(result => result.Success && !result.IsLatestVersion && !string.IsNullOrWhiteSpace(result.LatestVersion))
                    .Select(result => (result.TemplatePackage.Identifier, result.TemplatePackage.Version, result.LatestVersion));

                var formatter =
                   TabularOutput.TabularOutput
                       .For(
                           new TabularOutputSettings(_engineEnvironmentSettings.Environment),
                           displayableResults)
                       .DefineColumn(r => r.Identifier, out object? packageColumn, LocalizableStrings.ColumnNamePackage, showAlways: true)
                       .DefineColumn(r => r.CurrentVersion ?? string.Empty, LocalizableStrings.ColumnNameCurrentVersion, showAlways: true)
                       .DefineColumn(r => r.LatestVersion ?? string.Empty, LocalizableStrings.ColumnNameLatestVersion, showAlways: true)
                       .OrderBy(packageColumn, StringComparer.CurrentCultureIgnoreCase);
                Reporter.Output.WriteLine(formatter.Layout());
                Reporter.Output.WriteLine();

                Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateSingleCommandHeader);
                Reporter.Output.WriteCommand(
                    Example
                        .For<NewCommand>(args.ParseResult)
                        .WithSubcommand<InstallCommand>()
                        .WithArgument(InstallCommand.NameArgument, $"<package>::<version>"));
                Reporter.Output.WriteCommand(
                      Example
                          .For<NewCommand>(args.ParseResult)
                          .WithSubcommand<InstallCommand>()
                          .WithArgument(InstallCommand.NameArgument, $"{displayableResults.First().Identifier}::{displayableResults.First().LatestVersion}"));
                Reporter.Output.WriteLine();
                Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateAllCommandHeader);
                Reporter.Output.WriteCommand(
                 Example
                     .For<NewCommand>(args.ParseResult)
                     .WithSubcommand<UpdateCommand>());
                Reporter.Output.WriteLine();
            }

            //handle errors
            if (versionCheckResults.Any(result => !result.Success))
            {
                foreach (CheckUpdateResult result in versionCheckResults.Where(result => !result.Success))
                {
                    // explicit check of updates requested - so we do not want to ignore errors for
                    //  local only packages
                    HandleUpdateCheckErrors(result, ignoreLocalPackageNotFound: false);
                }
                Reporter.Error.WriteLine();
            }
        }

        private async Task DisplayInstalledTemplatePackagesAsync(GlobalArgs args, CancellationToken cancellationToken)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<IManagedTemplatePackage> managedTemplatePackages = await _templatePackageManager.GetManagedTemplatePackagesAsync(false, cancellationToken).ConfigureAwait(false);

            Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Info_InstalledItems);

            if (!managedTemplatePackages.Any())
            {
                Reporter.Output.WriteLine(LocalizableStrings.NoItems);
                return;
            }

            foreach (IManagedTemplatePackage managedSource in managedTemplatePackages)
            {
                Reporter.Output.WriteLine($"{managedSource.Identifier}".Indent());
                if (!string.IsNullOrWhiteSpace(managedSource.Version))
                {
                    Reporter.Output.WriteLine($"{LocalizableStrings.Version} {managedSource.Version}".Indent(level: 2));
                }

                IReadOnlyDictionary<string, string> displayDetails = managedSource.GetDetails();
                if (displayDetails?.Any() ?? false)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Info_DetailsHeader.Indent(level: 2));
                    foreach (KeyValuePair<string, string> detail in displayDetails)
                    {
                        Reporter.Output.WriteLine($"{detail.Key}: {GetFormattedValue(detail.Value)}".Indent(level: 3));
                    }
                }

                IEnumerable<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(managedSource, cancellationToken).ConfigureAwait(false);
                if (templates.Any())
                {
                    Reporter.Output.WriteLine($"{LocalizableStrings.Templates}:".Indent(level: 2));
                    foreach (ITemplateInfo info in templates)
                    {
                        Reporter.Output.WriteLine(info.GetDisplayName().Indent(level: 3));
                    }
                }

                // uninstall command:
                Reporter.Output.WriteLine($"{LocalizableStrings.TemplatePackageCoordinator_Uninstall_Info_UninstallCommandHint}".Indent(level: 2));
                Reporter.Output.WriteCommand(
                    Example
                        .For<NewCommand>(args.ParseResult)
                        .WithSubcommand<UninstallCommand>()
                        .WithArgument(UninstallCommand.NameArgument, managedSource.Identifier),
                    indentLevel: 2);

                Reporter.Output.WriteLine();
            }
        }

        private string GetFormattedValue(string rawValue)
        {
            if (bool.TryParse(rawValue, out bool value))
            {
                return value ? "✔" : "✘";
            }

            return rawValue;
        }

        private async Task DisplayInstallResultAsync(string packageToInstall, InstallerOperationResult result, ParseResult parseResult, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageToInstall))
            {
                throw new ArgumentException(nameof(packageToInstall));
            }
            _ = result ?? throw new ArgumentNullException(nameof(result));
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Success)
            {
                if (result.TemplatePackage is null)
                {
                    throw new ArgumentException($"{nameof(result.TemplatePackage)} cannot be null when {nameof(result.Success)} is 'true'", nameof(result));
                }
                IEnumerable<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(result.TemplatePackage, cancellationToken).ConfigureAwait(false);
                if (templates.Any())
                {
                    Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_lnstall_Info_Success, result.TemplatePackage.DisplayName);
                    TemplateGroupDisplay.DisplayTemplateList(
                        _engineEnvironmentSettings,
                        templates,
                        new TabularOutputSettings(_engineEnvironmentSettings.Environment),
                        reporter: Reporter.Output);
                    await EvaluateAndDisplayConstraintsAsync(templates, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_lnstall_Warning_No_Templates_In_Package, result.TemplatePackage.DisplayName);
                }
            }
            else
            {
                switch (result.Error)
                {
                    case InstallerErrorCode.InvalidSource:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_InvalidNuGetFeeds,
                                packageToInstall,
                                result.ErrorMessage).Bold().Red());
                        break;
                    case InstallerErrorCode.PackageNotFound:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_PackageNotFound,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.DownloadFailed:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_DownloadFailed,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.UnsupportedRequest:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_UnsupportedRequest,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.AlreadyInstalled:
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_AlreadyInstalled,
                                  packageToInstall).Bold().Red());
                        Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_AlreadyInstalled_Hint, InstallCommand.ForceOption.Aliases.First());
                        Reporter.Error.WriteCommand(Example.For<InstallCommand>(parseResult).WithArgument(BaseInstallCommand.NameArgument, packageToInstall).WithOption(BaseInstallCommand.ForceOption));

                        break;
                    case InstallerErrorCode.UpdateUninstallFailed:
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_UninstallFailed,
                                  packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.InvalidPackage:
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_InvalidPackage,
                                  packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.GenericError:
                    default:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_GenericError,
                                packageToInstall).Bold().Red());
                        break;
                }
            }
        }

        private async Task EvaluateAndDisplayConstraintsAsync(IEnumerable<ITemplateInfo> templates, CancellationToken cancellationToken)
        {
            var evaluationResult = await _constraintsManager.EvaluateConstraintsAsync(templates, cancellationToken).ConfigureAwait(false);

            var restrictedTemplates = evaluationResult.Where(r => r.Result.Any(cr => cr.EvaluationStatus != TemplateConstraintResult.Status.Allowed));
            if (!restrictedTemplates.Any())
            {
                return;
            }

            Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_ConstraintsNotice);

            foreach (var template in restrictedTemplates)
            {
                bool showIdentity = !string.IsNullOrWhiteSpace(template.Template.GroupIdentity) && templates.Count(t => t.GroupIdentity == template.Template.GroupIdentity) > 1;
                Reporter.Output.WriteLine(template.Template.GetDisplayName(showIdentity: showIdentity));
                foreach (var constraintResult in template.Result.Where(r => r.EvaluationStatus != TemplateConstraintResult.Status.Allowed))
                {
                    Reporter.Output.WriteLine(constraintResult.ToDisplayString().Indent(1));
                }
            }
        }

        private void HandleUpdateCheckErrors(CheckUpdateResult result, bool ignoreLocalPackageNotFound)
        {
            switch (result.Error)
            {
                case InstallerErrorCode.InvalidSource:
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_Update_Error_InvalidNuGetFeeds,
                            result.TemplatePackage.DisplayName).Bold().Red());
                    break;
                case InstallerErrorCode.PackageNotFound:
                    if (!ignoreLocalPackageNotFound || !result.TemplatePackage.IsLocalPackage)
                    {
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_Update_Error_PackageNotFound,
                                result.TemplatePackage.DisplayName).Bold().Red());
                    }
                    break;
                case InstallerErrorCode.UnsupportedRequest:
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_Update_Error_PackageNotSupported,
                            result.TemplatePackage.DisplayName).Bold().Red());
                    break;
                case InstallerErrorCode.GenericError:
                default:
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_Update_Error_GenericError,
                            result.TemplatePackage.DisplayName,
                            result.ErrorMessage).Bold().Red());
                    break;
            }
        }

        private IEnumerable<PackageSource> LoadNuGetSources(IEnumerable<string>? additionalSources, bool includeNuGetFeed)
        {
            IEnumerable<PackageSource> defaultSources;
            string currentDirectory = string.Empty;
            try
            {
                currentDirectory = Directory.GetCurrentDirectory();
                ISettings settings = global::NuGet.Configuration.Settings.LoadDefaultSettings(currentDirectory);
                PackageSourceProvider packageSourceProvider = new PackageSourceProvider(settings);
                defaultSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
                if (includeNuGetFeed)
                {
                    var nuGetFeed = new PackageSource(NugetOrgFeed, "NuGet.org");
                    defaultSources = defaultSources.Append(nuGetFeed);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(LocalizableStrings.DetailsCommand_UnableToLoadResorces, currentDirectory), ex);
            }

            if (additionalSources == null || !additionalSources.Any())
            {
                if (!defaultSources.Any())
                {
                    throw new Exception(LocalizableStrings.DetailsCommand_NoNuGetSources);
                }
                return defaultSources;
            }

            List<PackageSource> customSources = new List<PackageSource>();
            foreach (string source in additionalSources)
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }
                if (defaultSources.Any(s => s.Source.Equals(source, StringComparison.OrdinalIgnoreCase)))
                {
                    Reporter.Verbose.WriteLine($"Custom source {source} is already loaded from default configuration.");
                    continue;
                }
                PackageSource packageSource = new PackageSource(source);
                if (packageSource.TrySourceAsUri == null)
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.DetailsCommand_UnableToLoadResorce, source));
                    continue;
                }
                customSources.Add(packageSource);
            }

            IEnumerable<PackageSource> retrievedSources = customSources.Concat(defaultSources);
            if (!retrievedSources.Any())
            {
                throw new Exception(LocalizableStrings.DetailsCommand_NoNuGetSources);
            }
            return retrievedSources;
        }

        private async Task<NugetPackageMetadata?> GetPackageMetadataFromMultipleFeedsAsync(
            IEnumerable<PackageSource> sources,
            NugetApiManager apiManager,
            string packageIdentifier,
            string? packageVersion = null,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<NugetPackageMetadata?> foundPackages =
            await Task.WhenAll(
                sources.Select(source => apiManager.GetPackageMetadataAsync(packageIdentifier, packageVersion, source, cancellationToken)))
                        .ConfigureAwait(false);

            var accumulativeSearchResults = foundPackages
                .Where(result => result is not null);

            if (accumulativeSearchResults == null || !accumulativeSearchResults.Any())
            {
                return null;
            }

            var floatRange = new FloatRange(NuGetVersionFloatBehavior.AbsoluteLatest);

            NugetPackageMetadata? latestVersion = accumulativeSearchResults.Aggregate(
                (NugetPackageMetadata?)null,
                (max, current) =>
                    (max == null || current!.Identity.Version > max.Identity.Version)
                    &&
                    floatRange.Satisfies(current!.Identity.Version) ?
                        current : max);

            return latestVersion;
        }
    }
}
