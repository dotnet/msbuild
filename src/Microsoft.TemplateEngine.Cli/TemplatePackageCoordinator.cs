// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using NuGet.Credentials;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The class is responsible for template package manipulation flows: install template packages (-i, --install), check for update (--update-check), apply updates (--update-apply), uninstall template packages (-u, --uninstall).
    /// </summary>
    internal class TemplatePackageCoordinator
    {
        private ITelemetryLogger _telemetryLogger;
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        private string _defaultLanguage;

        internal TemplatePackageCoordinator(
            ITelemetryLogger telemetryLogger,
            IEngineEnvironmentSettings environmentSettings,
            string? defaultLanguage = null)
        {
            _ = telemetryLogger ?? throw new ArgumentNullException(nameof(telemetryLogger));
            _ = environmentSettings ?? throw new ArgumentNullException(nameof(environmentSettings));
            if (string.IsNullOrWhiteSpace(defaultLanguage))
            {
                defaultLanguage = string.Empty;
            }

            _telemetryLogger = telemetryLogger;
            _engineEnvironmentSettings = environmentSettings;
            _defaultLanguage = defaultLanguage!;
        }

        /// <summary>
        /// Checks if <paramref name="commandInput"/> has instructions for template packages.
        /// </summary>
        /// <param name="commandInput">the command input to check.</param>
        /// <returns></returns>
        internal static bool IsTemplatePackageManipulationFlow(INewCommandInput commandInput)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));

            if (commandInput.CheckForUpdates || commandInput.ApplyUpdates)
            {
                return true;
            }
            if (commandInput.ToUninstallList != null)
            {
                return true;
            }
            if (commandInput.ToInstallList != null && commandInput.ToInstallList.Count > 0 && commandInput.ToInstallList[0] != null)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Processes template packages according to <paramref name="commandInput"/>.
        /// </summary>
        /// <param name="commandInput">the command input with instructions to process.</param>
        /// <returns></returns>
        internal Task<CreationResultStatus> ProcessAsync(INewCommandInput commandInput, CancellationToken cancellationToken = default)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
            cancellationToken.ThrowIfCancellationRequested();

            if (commandInput.ToUninstallList != null)
            {
                return EnterUninstallFlowAsync(commandInput, cancellationToken);
            }

            if (commandInput.CheckForUpdates || commandInput.ApplyUpdates)
            {
                InitializeNuGetCredentialService(commandInput);
                return EnterUpdateFlowAsync(commandInput, cancellationToken);
            }
            if (commandInput.ToInstallList != null && commandInput.ToInstallList.Count > 0 && commandInput.ToInstallList[0] != null)
            {
                InitializeNuGetCredentialService(commandInput);
                return EnterInstallFlowAsync(commandInput, cancellationToken);
            }
            throw new NotSupportedException($"The operation is not supported, command: {commandInput}.");
        }

        /// <summary>
        /// Checks if there is an update for the package containing the <paramref name="template"/>.
        /// </summary>
        /// <param name="template">template to check the update for.</param>
        /// <param name="commandInput"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Task for checking the update or null when check for update is not possible.</returns>
        internal async Task<CheckUpdateResult?> CheckUpdateForTemplate(ITemplateInfo template, INewCommandInput commandInput, CancellationToken cancellationToken = default)
        {
            _ = template ?? throw new ArgumentNullException(nameof(template));
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
            cancellationToken.ThrowIfCancellationRequested();

            ITemplatePackage templatePackage;
            try
            {
                templatePackage = await template.GetTemplatePackageAsync(_engineEnvironmentSettings).ConfigureAwait(false);
            }
            catch (Exception)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Error_PackageForTemplateNotFound, template.Identity));
                return null;
            }

            if (!(templatePackage is IManagedTemplatePackage managedTemplatePackage))
            {
                //update is not supported - built-in or optional workload source
                return null;
            }

            InitializeNuGetCredentialService(commandInput);
            return (await managedTemplatePackage.ManagedProvider.GetLatestVersionsAsync(new[] { managedTemplatePackage }, cancellationToken).ConfigureAwait(false)).Single();
        }

        internal void DisplayUpdateCheckResult(CheckUpdateResult versionCheckResult, INewCommandInput commandInput)
        {
            _ = versionCheckResult ?? throw new ArgumentNullException(nameof(versionCheckResult));
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));

            if (versionCheckResult.Success)
            {
                if (!versionCheckResult.IsLatestVersion)
                {
                    string displayString = $"{versionCheckResult.TemplatePackage.Identifier}::{versionCheckResult.TemplatePackage.Version}";         // the package::version currently installed
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Update_Info_UpdateAvailable, displayString));
                    string installString = $"{versionCheckResult.TemplatePackage.Identifier}::{versionCheckResult.LatestVersion}"; // the package::version that will be installed
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Update_Info_InstallCommand, commandInput.CommandName, installString));
                }
            }
            else
            {
                HandleUpdateCheckErrors(versionCheckResult);
            }
        }

        private static void InitializeNuGetCredentialService(INewCommandInput commandInput)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));

            try
            {
                DefaultCredentialServiceUtility.SetupDefaultCredentialService(new CliNuGetLogger(), !commandInput.IsInteractiveFlagSpecified);
            }
            catch (Exception ex)
            {
                Reporter.Verbose.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatesPackageCoordinator_Verbose_NuGetCredentialServiceError,
                        ex.ToString()));
            }
        }

        /// <summary>
        /// Install the template package(s) flow (--install, -i).
        /// </summary>
        private async Task<CreationResultStatus> EnterInstallFlowAsync(INewCommandInput commandInput, CancellationToken cancellationToken)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
            cancellationToken.ThrowIfCancellationRequested();

            CreationResultStatus resultStatus = CreationResultStatus.Success;
            _telemetryLogger.TrackEvent(commandInput.CommandName + TelemetryConstants.InstallEventSuffix, new Dictionary<string, string> { { TelemetryConstants.ToInstallCount, commandInput.ToInstallList.Count.ToString() } });

            var details = new Dictionary<string, string>();
            if (commandInput.InstallNuGetSourceList?.Count > 0)
            {
                details[InstallerConstants.NuGetSourcesKey] = string.Join(InstallerConstants.NuGetSourcesSeparator.ToString(), commandInput.InstallNuGetSourceList);
            }
            if (commandInput.IsInteractiveFlagSpecified)
            {
                details[InstallerConstants.InteractiveModeKey] = "true";
            }

            // In future we might want give user ability to pick IManagerSourceProvider by Name or GUID
            var managedSourceProvider = _engineEnvironmentSettings.SettingsLoader.TemplatePackagesManager.GetBuiltInManagedProvider(InstallationScope.Global);
            List<InstallRequest> installRequests = new List<InstallRequest>();

            foreach (string installArg in commandInput.ToInstallList)
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
                Reporter.Error.WriteLine(LocalizableStrings.TemplatesPackageCoordinator_Install_Error_FoundNoPackagesToInstall);
                return CreationResultStatus.NotFound;
            }

            //validate if installation requests have unique identifier
            HashSet<string> identifiers = new HashSet<string>();
            foreach (InstallRequest installRequest in installRequests)
            {
                if (identifiers.Add(installRequest.PackageIdentifier))
                {
                    continue;
                }
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Install_Error_SameInstallRequests, installRequest.PackageIdentifier));
                return CreationResultStatus.Cancelled;
            }

            Reporter.Output.WriteLine(LocalizableStrings.TemplatesPackageCoordinator_Install_Info_PackagesToBeInstalled);
            foreach (InstallRequest installRequest in installRequests)
            {
                if (string.IsNullOrWhiteSpace(installRequest.Version))
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Info_PackageName, installRequest.PackageIdentifier));
                }
                else
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Info_PackageNameVersion, installRequest.PackageIdentifier, installRequest.Version));
                }
            }
            Reporter.Output.WriteLine();

            IReadOnlyList<InstallResult> installResults = await managedSourceProvider.InstallAsync(installRequests, cancellationToken).ConfigureAwait(false);
            foreach (InstallResult result in installResults)
            {
                await DisplayInstallResultAsync(commandInput, result.InstallRequest.DisplayName, result, cancellationToken).ConfigureAwait(false);
                if (!result.Success)
                {
                    resultStatus = CreationResultStatus.CreateFailed;
                }
            }
            return resultStatus;
        }

        /// <summary>
        /// Update the template package(s) flow (--update-check and --update-apply).
        /// </summary>
        private async Task<CreationResultStatus> EnterUpdateFlowAsync(INewCommandInput commandInput, CancellationToken cancellationToken)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
            cancellationToken.ThrowIfCancellationRequested();

            bool applyUpdates = commandInput.ApplyUpdates;
            bool allTemplatesUpToDate = true;
            CreationResultStatus success = CreationResultStatus.Success;
            var managedTemplatePackages = await _engineEnvironmentSettings.SettingsLoader.TemplatePackagesManager.GetManagedTemplatePackagesAsync().ConfigureAwait(false);

            foreach (var packagesGrouping in managedTemplatePackages.GroupBy(package => package.ManagedProvider))
            {
                var provider = packagesGrouping.Key;
                IReadOnlyList<CheckUpdateResult> checkUpdateResults = await provider.GetLatestVersionsAsync(packagesGrouping, cancellationToken).ConfigureAwait(false);
                DisplayUpdateCheckResults(checkUpdateResults, commandInput, showUpdates: !applyUpdates);
                if (checkUpdateResults.Any(result => !result.Success))
                {
                    success = CreationResultStatus.CreateFailed;
                }
                allTemplatesUpToDate = checkUpdateResults.All(result => result.Success && result.IsLatestVersion);

                if (applyUpdates)
                {
                    IEnumerable<CheckUpdateResult> updatesToApply = checkUpdateResults.Where(update => update.Success && !update.IsLatestVersion && !string.IsNullOrWhiteSpace(update.LatestVersion));
                    if (!updatesToApply.Any())
                    {
                        continue;
                    }

                    Reporter.Output.WriteLine(LocalizableStrings.TemplatesPackageCoordinator_Update_Info_PackagesToBeUpdated);
                    foreach (CheckUpdateResult update in updatesToApply)
                    {
                        Reporter.Output.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatesPackageCoordinator_Info_PackageNameVersion,
                                update.TemplatePackage.Identifier,
                                update.LatestVersion));
                    }
                    Reporter.Output.WriteLine();

                    IReadOnlyList<UpdateResult> updateResults = await provider.UpdateAsync(updatesToApply.Select(update => new UpdateRequest(update.TemplatePackage, update.LatestVersion)), cancellationToken).ConfigureAwait(false);
                    foreach (var updateResult in updateResults)
                    {
                        if (!updateResult.Success)
                        {
                            success = CreationResultStatus.CreateFailed;
                        }
                        await DisplayInstallResultAsync(commandInput, updateResult.UpdateRequest.TemplatePackage.DisplayName, updateResult, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (allTemplatesUpToDate)
            {
                Reporter.Output.WriteLine(LocalizableStrings.TemplatesPackageCoordinator_Update_Info_AllPackagesAreUpToDate);
            }

            return success;
        }

        /// <summary>
        /// Uninstall the template package(s) flow (--uninstall, -u).
        /// </summary>
        private async Task<CreationResultStatus> EnterUninstallFlowAsync(INewCommandInput commandInput, CancellationToken cancellationToken)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
            cancellationToken.ThrowIfCancellationRequested();

            CreationResultStatus result = CreationResultStatus.Success;
            if (commandInput.ToUninstallList.Count <= 0 || commandInput.ToUninstallList[0] == null)
            {
                //display all installed template packages
                await DisplayInstalledTemplatePackages(commandInput, cancellationToken).ConfigureAwait(false);
                return result;
            }

            Dictionary<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>> sourcesToUninstall;
            (result, sourcesToUninstall) = await DetermineSourcesToUninstall(commandInput, cancellationToken).ConfigureAwait(false);

            foreach (KeyValuePair<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>> providerSourcesToUninstall in sourcesToUninstall)
            {
                IReadOnlyList<UninstallResult> uninstallResults = await providerSourcesToUninstall.Key.UninstallAsync(providerSourcesToUninstall.Value, cancellationToken).ConfigureAwait(false);
                foreach (UninstallResult uninstallResult in uninstallResults)
                {
                    if (uninstallResult.Success)
                    {
                        Reporter.Output.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatesPackageCoordinator_Uninstall_Info_Success,
                                uninstallResult.TemplatePackage.DisplayName));
                    }
                    else
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Uninstall_Error_GenericError, uninstallResult.TemplatePackage.DisplayName, uninstallResult.ErrorMessage));
                        result = CreationResultStatus.CreateFailed;
                    }
                }
            }
            return result;
        }

        private async Task<(CreationResultStatus, Dictionary<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>>)> DetermineSourcesToUninstall(INewCommandInput commandInput, CancellationToken cancellationToken)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
            cancellationToken.ThrowIfCancellationRequested();

            CreationResultStatus result = CreationResultStatus.Success;
            IReadOnlyList<IManagedTemplatePackage> templatePackages = await _engineEnvironmentSettings.SettingsLoader.TemplatePackagesManager.GetManagedTemplatePackagesAsync().ConfigureAwait(false);

            var packagesToUninstall = new Dictionary<IManagedTemplatePackageProvider, List<IManagedTemplatePackage>>();
            foreach (string templatePackageIdentifier in commandInput.ToUninstallList)
            {
                bool templatePackageIdentified = false;

                foreach (IManagedTemplatePackage templatePackage in templatePackages)
                {
                    if (templatePackage.Identifier.Equals(templatePackageIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        templatePackageIdentified = true;
                        if (packagesToUninstall.TryGetValue(templatePackage.ManagedProvider, out List<IManagedTemplatePackage> packages))
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

                result = CreationResultStatus.NotFound;
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatesPackageCoordinator_Error_PackageNotFound,
                        templatePackageIdentifier).Bold().Red());
                if (await IsTemplateShortNameAsync(templatePackageIdentifier, cancellationToken).ConfigureAwait(false))
                {
                    var packages = await GetTemplatePackagesByShortNameAsync(templatePackageIdentifier, cancellationToken).ConfigureAwait(false);
                    var managedPackages = packages.OfType<IManagedTemplatePackage>();
                    if (managedPackages.Any())
                    {
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatesPackageCoordinator_Error_TemplateIncludedToPackages,
                                  templatePackageIdentifier).Bold().Red());
                        foreach (IManagedTemplatePackage managedPackage in managedPackages)
                        {
                            IEnumerable<ITemplateInfo> templates = await managedPackage.GetTemplates(_engineEnvironmentSettings).ConfigureAwait(false);
                            Reporter.Error.WriteLine(
                                  string.Format(
                                      LocalizableStrings.TemplatesPackageCoordinator_Error_PackageNameContainsTemplates,
                                      managedPackage.DisplayName,
                                      templates.Count()).Bold().Red());
                        }
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatesPackageCoordinator_Uninstall_Error_UninstallCommandHint,
                                  commandInput.CommandName,
                                  managedPackages?.First().Identifier).Bold().Red());
                        //TODO:
                        //Reporter.Error.WriteLine($"To list the templates installed in a package, use dotnet new3 <new option> <package name>.");
                    }
                    else
                    {
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatesPackageCoordinator_Uninstall_Error_ListPackagesHint,
                                  commandInput.CommandName).Bold().Red());
                    }
                }
                else
                {
                    Reporter.Error.WriteLine(
                          string.Format(
                              LocalizableStrings.TemplatesPackageCoordinator_Uninstall_Error_ListPackagesHint,
                              commandInput.CommandName));
                }
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

            IReadOnlyList<ITemplateInfo> templates = await _engineEnvironmentSettings.SettingsLoader.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            var templatesWithMatchedShortName = templates.Where(template =>
            {
                return template.ShortNameList.Contains(sourceIdentifier, StringComparer.OrdinalIgnoreCase);
            });

            var templatePackages = await Task.WhenAll(
                templatesWithMatchedShortName.Select(
                    t => t.GetTemplatePackageAsync(_engineEnvironmentSettings)))
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

            IReadOnlyList<ITemplateInfo> templates = await _engineEnvironmentSettings.SettingsLoader.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            return templates.Any(template =>
            {
                return template.ShortNameList.Contains(sourceIdentifier, StringComparer.OrdinalIgnoreCase);
            });
        }

        private void DisplayUpdateCheckResults(IEnumerable<CheckUpdateResult> versionCheckResults, INewCommandInput commandInput, bool showUpdates = true)
        {
            _ = versionCheckResults ?? throw new ArgumentNullException(nameof(versionCheckResults));
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));

            foreach (CheckUpdateResult result in versionCheckResults)
            {
                if (result.Success)
                {
                    if (!result.IsLatestVersion && showUpdates)
                    {
                        string displayString = $"{result.TemplatePackage.Identifier}::{result.TemplatePackage.Version}";         // the package::version currently installed
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Update_Info_UpdateAvailable, displayString));
                        string installString = $"{result.TemplatePackage.Identifier}::{result.LatestVersion}"; // the package::version that will be installed
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatesPackageCoordinator_Update_Info_InstallCommand, commandInput.CommandName, installString));
                    }
                }
                else
                {
                    HandleUpdateCheckErrors(result);
                }
            }
        }

        private async Task DisplayInstalledTemplatePackages(INewCommandInput commandInput, CancellationToken cancellationToken)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<IManagedTemplatePackage> managedTemplatePackages = await _engineEnvironmentSettings.SettingsLoader.TemplatePackagesManager.GetManagedTemplatePackagesAsync().ConfigureAwait(false);

            Reporter.Output.WriteLine(LocalizableStrings.TemplatesPackageCoordinator_Uninstall_Info_InstalledItems);

            if (!managedTemplatePackages.Any())
            {
                Reporter.Output.WriteLine(LocalizableStrings.NoItems);
                return;
            }

            foreach (IManagedTemplatePackage managedSource in managedTemplatePackages)
            {
                Reporter.Output.WriteLine($"  {managedSource.Identifier}");
                if (!string.IsNullOrWhiteSpace(managedSource.Version))
                {
                    Reporter.Output.WriteLine($"    {LocalizableStrings.Version} {managedSource.Version}");
                }

                IReadOnlyDictionary<string, string> displayDetails = managedSource.GetDetails();
                if (displayDetails?.Any() ?? false)
                {
                    Reporter.Output.WriteLine($"    {LocalizableStrings.TemplatesPackageCoordinator_Uninstall_Info_DetailsHeader}");
                    foreach (KeyValuePair<string, string> detail in displayDetails)
                    {
                        Reporter.Output.WriteLine($"      {detail.Key}: {detail.Value}");
                    }
                }

                IEnumerable<ITemplateInfo> templates = await managedSource.GetTemplates(_engineEnvironmentSettings).ConfigureAwait(false);
                if (templates.Any())
                {
                    Reporter.Output.WriteLine($"    {LocalizableStrings.Templates}:");
                    foreach (TemplateInfo info in templates)
                    {
                        string templateLanguage = info.GetLanguage();
                        string shortNames = string.Join(",", info.ShortNameList);
                        if (!string.IsNullOrWhiteSpace(templateLanguage))
                        {
                            Reporter.Output.WriteLine($"      {info.Name} ({shortNames}) {templateLanguage}");
                        }
                        else
                        {
                            Reporter.Output.WriteLine($"      {info.Name} ({shortNames})");
                        }
                    }
                }

                // uninstall command:
                Reporter.Output.WriteLine($"    {LocalizableStrings.TemplatesPackageCoordinator_Uninstall_Info_UninstallCommandHint}");
                Reporter.Output.WriteLine(string.Format("      dotnet {0} -u {1}", commandInput.CommandName, managedSource.Identifier));

                Reporter.Output.WriteLine();
            }
        }

        private async Task DisplayInstallResultAsync(INewCommandInput commandInput, string packageToInstall, InstallerOperationResult result, CancellationToken cancellationToken)
        {
            _ = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
            if (string.IsNullOrWhiteSpace(packageToInstall))
            {
                throw new ArgumentException(nameof(packageToInstall));
            }
            _ = result ?? throw new ArgumentNullException(nameof(result));
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Success)
            {
                Reporter.Output.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatesPackageCoordinator_lnstall_Info_Success,
                        result.TemplatePackage.DisplayName));
                IEnumerable<ITemplateInfo> templates = await result.TemplatePackage.GetTemplates(_engineEnvironmentSettings).ConfigureAwait(false);
                HelpForTemplateResolution.DisplayTemplateList(templates, _engineEnvironmentSettings, commandInput, _defaultLanguage);
            }
            else
            {
                switch (result.Error)
                {
                    case InstallerErrorCode.InvalidSource:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatesPackageCoordinator_lnstall_Error_InvalidNuGetFeeds,
                                packageToInstall,
                                result.ErrorMessage).Bold().Red());
                        break;
                    case InstallerErrorCode.PackageNotFound:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatesPackageCoordinator_lnstall_Error_PackageNotFound,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.DownloadFailed:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatesPackageCoordinator_lnstall_Error_DownloadFailed,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.UnsupportedRequest:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatesPackageCoordinator_lnstall_Error_UnsupportedRequest,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.AlreadyInstalled:
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatesPackageCoordinator_lnstall_Error_AlreadyInstalled,
                                  packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.UpdateUninstallFailed:
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatesPackageCoordinator_lnstall_Error_UninstallFailed,
                                  packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.InvalidPackage:
                        Reporter.Error.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatesPackageCoordinator_lnstall_Error_InvalidPackage,
                                  packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.GenericError:
                    default:
                        Reporter.Error.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatesPackageCoordinator_lnstall_Error_GenericError,
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
                            LocalizableStrings.TemplatesPackageCoordinator_Update_Error_InvalidNuGetFeeds,
                            result.TemplatePackage.DisplayName).Bold().Red());
                    break;
                case InstallerErrorCode.PackageNotFound:
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatesPackageCoordinator_Update_Error_PackageNotFound,
                            result.TemplatePackage.DisplayName).Bold().Red());
                    break;
                case InstallerErrorCode.UnsupportedRequest:
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatesPackageCoordinator_Update_Error_PackageNotSupported,
                            result.TemplatePackage.DisplayName).Bold().Red());
                    break;
                case InstallerErrorCode.GenericError:
                default:
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatesPackageCoordinator_Update_Error_GenericError,
                            result.TemplatePackage.DisplayName,
                            result.ErrorMessage).Bold().Red());
                    break;
            }
        }
    }
}
