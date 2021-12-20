// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using NuGet.Credentials;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The class is responsible for template package manipulation flows: install template packages (-i, --install), check for update (--update-check), apply updates (--update-apply), uninstall template packages (-u, --uninstall).
    /// </summary>
    internal class TemplatePackageCoordinator
    {
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly TemplatePackageManager _templatePackageManager;

        internal TemplatePackageCoordinator(
            ITelemetryLogger telemetryLogger,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager)
        {
            _telemetryLogger = telemetryLogger ?? throw new ArgumentNullException(nameof(telemetryLogger));
            _engineEnvironmentSettings = environmentSettings ?? throw new ArgumentNullException(nameof(environmentSettings));
            _templatePackageManager = templatePackageManager ?? throw new ArgumentNullException(nameof(templatePackageManager));
        }

        /// <summary>
        /// Checks if there is an update for the package containing the <paramref name="template"/>.
        /// </summary>
        /// <param name="template">template to check the update for.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Task for checking the update or null when check for update is not possible.</returns>
        internal async Task<CheckUpdateResult?> CheckUpdateForTemplate(ITemplateInfo template, CancellationToken cancellationToken = default)
        {
            _ = template ?? throw new ArgumentNullException(nameof(template));
            cancellationToken.ThrowIfCancellationRequested();

            ITemplatePackage templatePackage;
            try
            {
                templatePackage = await _templatePackageManager.GetTemplatePackageAsync(template, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplatePackageCoordinator_Error_PackageForTemplateNotFound, template.Identity));
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

        internal void DisplayUpdateCheckResult(CheckUpdateResult versionCheckResult, string commandName)
        {
            _ = versionCheckResult ?? throw new ArgumentNullException(nameof(versionCheckResult));

            if (versionCheckResult.Success)
            {
                if (!versionCheckResult.IsLatestVersion)
                {
                    string displayString = $"{versionCheckResult.TemplatePackage.Identifier}::{versionCheckResult.TemplatePackage.Version}";         // the package::version currently installed
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateAvailable, displayString));

                    Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateSingleCommandHeader);
                    Reporter.Output.WriteCommand(CommandExamples.InstallCommandExample(
                        commandName,
                        packageID: versionCheckResult.TemplatePackage.Identifier,
                        version: versionCheckResult.LatestVersion));
                }
            }
            else
            {
                HandleUpdateCheckErrors(versionCheckResult);
            }
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
            _telemetryLogger.TrackEvent(args.CommandName + TelemetryConstants.InstallEventSuffix, new Dictionary<string, string> { { TelemetryConstants.ToInstallCount, args.TemplatePackages.Count.ToString() } });

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
                string? version = null;
                //'*' is placeholder for the latest version
                if (splitByColons.Length > 1 && splitByColons[1] != "*")
                {
                    version = splitByColons[1];
                }
                foreach (string expandedIdentifier in InstallRequestPathResolution.ExpandMaskedPath(identifier, _engineEnvironmentSettings))
                {
                    installRequests.Add(new InstallRequest(expandedIdentifier, version, details: details));
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
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplatePackageCoordinator_Install_Error_SameInstallRequests, installRequest.PackageIdentifier));
                return NewCommandStatus.Cancelled;
            }

            Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_Info_PackagesToBeInstalled);
            foreach (InstallRequest installRequest in installRequests)
            {
                Reporter.Output.WriteLine(installRequest.DisplayName.Indent());
            }
            Reporter.Output.WriteLine();

            IReadOnlyList<InstallResult> installResults = await managedSourceProvider.InstallAsync(installRequests, cancellationToken).ConfigureAwait(false);
            foreach (InstallResult result in installResults)
            {
                await DisplayInstallResultAsync(result.InstallRequest.DisplayName, result, cancellationToken).ConfigureAwait(false);
                if (!result.Success)
                {
                    resultStatus = NewCommandStatus.CreateFailed;
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
                DisplayUpdateCheckResults(checkUpdateResults, commandArgs.CommandName, showUpdates: !applyUpdates);
                if (checkUpdateResults.Any(result => !result.Success))
                {
                    success = NewCommandStatus.CreateFailed;
                }
                allTemplatesUpToDate = checkUpdateResults.All(result => result.Success && result.IsLatestVersion);

                if (applyUpdates)
                {
                    IEnumerable<CheckUpdateResult> updatesToApply = checkUpdateResults.Where(update => update.Success && !update.IsLatestVersion && !string.IsNullOrWhiteSpace(update.LatestVersion));
                    if (!updatesToApply.Any())
                    {
                        continue;
                    }

                    Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_PackagesToBeUpdated);
                    foreach (CheckUpdateResult update in updatesToApply)
                    {
                        Reporter.Output.WriteLine($"{update.TemplatePackage.Identifier}::{update.LatestVersion}".Indent());
                    }
                    Reporter.Output.WriteLine();

                    IReadOnlyList<UpdateResult> updateResults = await provider.UpdateAsync(updatesToApply.Select(update => new UpdateRequest(update.TemplatePackage, update.LatestVersion)), cancellationToken).ConfigureAwait(false);
                    foreach (var updateResult in updateResults)
                    {
                        if (!updateResult.Success)
                        {
                            success = NewCommandStatus.CreateFailed;
                        }
                        await DisplayInstallResultAsync(updateResult.UpdateRequest.TemplatePackage.DisplayName, updateResult, cancellationToken).ConfigureAwait(false);
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
                await DisplayInstalledTemplatePackagesAsync(args, cancellationToken).ConfigureAwait(false);
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
                        Reporter.Output.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_Uninstall_Info_Success,
                                uninstallResult.TemplatePackage.DisplayName));
                    }
                    else
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Error_GenericError, uninstallResult.TemplatePackage.DisplayName, uninstallResult.ErrorMessage));
                        result = NewCommandStatus.CreateFailed;
                    }
                }
            }
            return result;
        }

        private static void InitializeNuGetCredentialService(bool interactive)
        {
            try
            {
                DefaultCredentialServiceUtility.SetupDefaultCredentialService(new CliNuGetLogger(), !interactive);
            }
            catch (Exception ex)
            {
                Reporter.Verbose.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatePackageCoordinator_Verbose_NuGetCredentialServiceError,
                        ex.ToString()));
            }
        }

        private async Task<(NewCommandStatus, Dictionary<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>>)> DetermineSourcesToUninstallAsync(UninstallCommandArgs commandArgs, CancellationToken cancellationToken)
        {
            _ = commandArgs ?? throw new ArgumentNullException(nameof(commandArgs));
            _ = commandArgs.TemplatePackages ?? throw new ArgumentNullException(nameof(commandArgs.TemplatePackages));
            cancellationToken.ThrowIfCancellationRequested();

            NewCommandStatus result = NewCommandStatus.Success;
            IReadOnlyList<IManagedTemplatePackage> templatePackages = await _templatePackageManager.GetManagedTemplatePackagesAsync(false, cancellationToken).ConfigureAwait(false);

            List<string> parsedIdentifiers = new List<string>();
            foreach (string entry in commandArgs.TemplatePackages)
            {
                parsedIdentifiers.AddRange(InstallRequestPathResolution.ExpandMaskedPath(entry, _engineEnvironmentSettings));
            }

            var packagesToUninstall = new Dictionary<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>>();
            foreach (string templatePackageIdentifier in parsedIdentifiers)
            {
                bool templatePackageIdentified = false;

                foreach (IManagedTemplatePackage templatePackage in templatePackages)
                {
                    if (templatePackage.Identifier.Equals(templatePackageIdentifier, StringComparison.OrdinalIgnoreCase))
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

                if (templatePackageIdentified)
                {
                    continue;
                }

                result = NewCommandStatus.NotFound;
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatePackageCoordinator_Error_PackageNotFound,
                        templatePackageIdentifier).Bold().Red());
                if (await IsTemplateShortNameAsync(templatePackageIdentifier, cancellationToken).ConfigureAwait(false))
                {
                    var packages = await GetTemplatePackagesByShortNameAsync(templatePackageIdentifier, cancellationToken).ConfigureAwait(false);
                    var managedPackages = packages.OfType<IManagedTemplatePackage>();
                    if (managedPackages.Any())
                    {
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatePackageCoordinator_Error_TemplateIncludedToPackages,
                                  templatePackageIdentifier));
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
                        Reporter.Error.WriteCommand(CommandExamples.UninstallCommandExample(commandArgs.CommandName, managedPackages?.First().Identifier ?? ""));
                        //TODO:
                        //Reporter.Error.WriteLine($"To list the templates installed in a package, use dotnet new3 <new option> <package name>.");
                    }
                    else
                    {
                        Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Error_ListPackagesHeader);
                        Reporter.Error.WriteCommand(CommandExamples.UninstallCommandExample(commandArgs.CommandName, noArgs: true));
                    }
                }
                else
                {
                    Reporter.Error.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Uninstall_Error_ListPackagesHeader);
                    Reporter.Error.WriteCommand(CommandExamples.UninstallCommandExample(commandArgs.CommandName, noArgs: true));
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

        private void DisplayUpdateCheckResults(IEnumerable<CheckUpdateResult> versionCheckResults, string commandName, bool showUpdates = true)
        {
            _ = versionCheckResults ?? throw new ArgumentNullException(nameof(versionCheckResults));

            //handle success
            if (versionCheckResults.Any(result => result.Success && !result.IsLatestVersion) && showUpdates)
            {
                Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateAvailablePackages);
                IEnumerable<(string Identifier, string CurrentVersion, string LatestVersion)> displayableResults = versionCheckResults
                    .Where(result => result.Success && !result.IsLatestVersion)
                    .Select(result => (result.TemplatePackage.Identifier, result.TemplatePackage.Version, result.LatestVersion));

                var formatter =
                   TabularOutput.TabularOutput
                       .For(
                           new TabularOutputSettings(_engineEnvironmentSettings.Environment),
                           displayableResults)
                       .DefineColumn(r => r.Identifier, out object packageColumn, LocalizableStrings.ColumnNamePackage, showAlways: true)
                       .DefineColumn(r => r.CurrentVersion, LocalizableStrings.ColumnNameCurrentVersion, showAlways: true)
                       .DefineColumn(r => r.LatestVersion, LocalizableStrings.ColumnNameLatestVersion, showAlways: true)
                       .OrderBy(packageColumn, StringComparer.CurrentCultureIgnoreCase);
                Reporter.Output.WriteLine(formatter.Layout());
                Reporter.Output.WriteLine();

                Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateSingleCommandHeader);
                Reporter.Output.WriteCommand(CommandExamples.InstallCommandExample(commandName, withVersion: true));
                Reporter.Output.WriteCommand(
                    CommandExamples.InstallCommandExample(
                        commandName,
                        packageID: displayableResults.First().Identifier,
                        version: displayableResults.First().LatestVersion));
                Reporter.Output.WriteLine();
                Reporter.Output.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateAllCommandHeader);
                Reporter.Output.WriteCommand(CommandExamples.UpdateApplyCommandExample(commandName));
                Reporter.Output.WriteLine();
            }

            //handle errors
            if (versionCheckResults.Any(result => !result.Success))
            {
                foreach (CheckUpdateResult result in versionCheckResults.Where(result => !result.Success))
                {
                    HandleUpdateCheckErrors(result);
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
                        Reporter.Output.WriteLine($"{detail.Key}: {detail.Value}".Indent(level: 3));
                    }
                }

                IEnumerable<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(managedSource, cancellationToken).ConfigureAwait(false);
                if (templates.Any())
                {
                    Reporter.Output.WriteLine($"{LocalizableStrings.Templates}:".Indent(level: 2));
                    foreach (ITemplateInfo info in templates)
                    {
                        string? templateLanguage = info.GetLanguage();
                        string shortNames = string.Join(",", info.ShortNameList);
                        if (!string.IsNullOrWhiteSpace(templateLanguage))
                        {
                            Reporter.Output.WriteLine($"{info.Name} ({shortNames}) {templateLanguage}".Indent(level: 3));
                        }
                        else
                        {
                            Reporter.Output.WriteLine($"{info.Name} ({shortNames})".Indent(level: 3));
                        }
                    }
                }

                // uninstall command:
                Reporter.Output.WriteLine($"{LocalizableStrings.TemplatePackageCoordinator_Uninstall_Info_UninstallCommandHint}".Indent(level: 2));
                Reporter.Output.WriteCommand(CommandExamples.UninstallCommandExample(args.CommandName, managedSource.Identifier), indentLevel: 2);

                Reporter.Output.WriteLine();
            }
        }

        private async Task DisplayInstallResultAsync(string packageToInstall, InstallerOperationResult result, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageToInstall))
            {
                throw new ArgumentException(nameof(packageToInstall));
            }
            _ = result ?? throw new ArgumentNullException(nameof(result));
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Success)
            {
                IEnumerable<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(result.TemplatePackage, cancellationToken).ConfigureAwait(false);
                if (templates.Any())
                {
                    Reporter.Output.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_lnstall_Info_Success,
                            result.TemplatePackage.DisplayName));
                    TemplateGroupDisplay.DisplayTemplateList(
                        _engineEnvironmentSettings,
                        templates,
                        new TabularOutputSettings(_engineEnvironmentSettings.Environment),
                        reporter: Reporter.Output);
                }
                else
                {
                    Reporter.Output.WriteLine(string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_lnstall_Warning_No_Templates_In_Package,
                            result.TemplatePackage.DisplayName));
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

        private void HandleUpdateCheckErrors(InstallerOperationResult result)
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
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_Update_Error_PackageNotFound,
                            result.TemplatePackage.DisplayName).Bold().Red());
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
    }
}
