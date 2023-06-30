// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The class is responsible for displaying template package manipulation results to console.
    /// </summary>
    internal class TemplatePackageDisplay
    {
        private readonly IReadOnlyDictionary<int, string> _vulnerabilitySeverityToColorMap = new Dictionary<int, string>
        {
            { 0, LocalizableStrings.TemplatePackageCoordinator_VulnerabilitySeverity_Low.Cyan() },
            { 1, LocalizableStrings.TemplatePackageCoordinator_VulnerabilitySeverity_Moderate.Blue() },
            { 2, LocalizableStrings.TemplatePackageCoordinator_VulnerabilitySeverity_High.Yellow() },
            { 3, LocalizableStrings.TemplatePackageCoordinator_VulnerabilitySeverity_Critical.Red() },
        };

        private readonly IReporter _reporterOutput;
        private readonly IReporter _reporterError;

        internal TemplatePackageDisplay(
           IReporter reporterOutput,
           IReporter reporterError)
        {
            _reporterOutput = reporterOutput;
            _reporterError = reporterError;
        }

        internal void DisplayUpdateCheckResult(CheckUpdateResult versionCheckResult, ICommandArgs args)
        {
            _ = versionCheckResult ?? throw new ArgumentNullException(nameof(versionCheckResult));

            if (versionCheckResult.Success)
            {
                if (!versionCheckResult.IsLatestVersion)
                {
                    string displayString = $"{versionCheckResult.TemplatePackage?.Identifier}::{versionCheckResult.TemplatePackage?.Version}";         // the package::version currently installed
                    _reporterOutput.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateAvailable, displayString);

                    _reporterOutput.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Update_Info_UpdateSingleCommandHeader);
                    _reporterOutput.WriteCommand(
                        Example
                            .For<NewCommand>(args.ParseResult)
                            .WithSubcommand<InstallCommand>()
                            .WithArgument(InstallCommand.NameArgument, $"{versionCheckResult.TemplatePackage?.Identifier}::{versionCheckResult.LatestVersion}"));
                    _reporterOutput.WriteLine();
                }
            }
            else
            {
                DisplayUpdateCheckErrors(versionCheckResult, ignoreLocalPackageNotFound: true);
                _reporterError.WriteLine();
            }
        }

        internal void DisplayBuiltInPackagesCheckResult(string packageId, string version, string provider, ICommandArgs args)
        {
            _reporterOutput.WriteLine(LocalizableStrings.TemplatePackageCoordinator_BuiltInCheck_Info_BuiltInPackageAvailable, $"{packageId}::{version}", provider);
            _reporterOutput.WriteLine(LocalizableStrings.TemplatePackageCoordinator_BuiltInCheck_Info_UninstallPackage);
            _reporterOutput.WriteCommand(
                Example
                 .For<NewCommand>(args.ParseResult)
                 .WithSubcommand<UninstallCommand>()
                 .WithArgument(UninstallCommand.NameArgument, packageId));
        }

        internal async Task DisplayInstallResultAsync(
            string packageToInstall,
            InstallerOperationResult result,
            ParseResult parseResult,
            bool force,
            TemplatePackageManager templatePackageManager,
            IEngineEnvironmentSettings engineEnvironmentSettings,
            TemplateConstraintManager constraintsManager,
            CancellationToken cancellationToken)
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
                IEnumerable<ITemplateInfo> templates = await templatePackageManager.GetTemplatesAsync(result.TemplatePackage, cancellationToken).ConfigureAwait(false);
                if (templates.Any())
                {
                    _reporterOutput.WriteLine(LocalizableStrings.TemplatePackageCoordinator_lnstall_Info_Success, result.TemplatePackage.DisplayName);
                    TemplateGroupDisplay.DisplayTemplateList(
                        engineEnvironmentSettings,
                        templates,
                        new TabularOutputSettings(engineEnvironmentSettings.Environment),
                        reporter: _reporterOutput);
                    await EvaluateAndDisplayConstraintsAsync(constraintsManager, templates, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _reporterOutput.WriteLine(LocalizableStrings.TemplatePackageCoordinator_lnstall_Warning_No_Templates_In_Package, result.TemplatePackage.DisplayName);
                }

                if (force && result is InstallResult installResult && installResult.Vulnerabilities.Any())
                {
                    _reporterOutput.WriteLine(string.Format(LocalizableStrings.TemplatePackageCoordinator_Download_VulnerablePackage));
                    DisplayVulnerabilityInfo(installResult.Vulnerabilities, _reporterOutput);
                }
            }
            else
            {
                switch (result.Error)
                {
                    case InstallerErrorCode.InvalidSource:
                        _reporterError.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_InvalidNuGetFeeds,
                                packageToInstall,
                                result.ErrorMessage).Bold().Red());
                        break;
                    case InstallerErrorCode.PackageNotFound:
                        _reporterError.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_PackageNotFound,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.DownloadFailed:
                        _reporterError.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_DownloadFailed,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.UnsupportedRequest:
                        _reporterError.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_UnsupportedRequest,
                                packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.AlreadyInstalled:
                        _reporterError.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_AlreadyInstalled,
                                  packageToInstall).Bold().Red());
                        _reporterError.WriteLine(LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_AlreadyInstalled_Hint, InstallCommand.ForceOption.Name);
                        _reporterError.WriteCommand(Example.For<InstallCommand>(parseResult).WithArgument(BaseInstallCommand.NameArgument, packageToInstall).WithOption(BaseInstallCommand.ForceOption));

                        break;
                    case InstallerErrorCode.UpdateUninstallFailed:
                        _reporterError.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_UninstallFailed,
                                  packageToInstall).Bold().Red());
                        break;
                    case InstallerErrorCode.InvalidPackage:
                        _reporterError.WriteLine(
                              string.Format(
                                  LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_InvalidPackage,
                                  packageToInstall).Bold().Red());
                        break;

                    case InstallerErrorCode.VulnerablePackage:
                        {
                            switch (result)
                            {
                                case InstallResult installResult when installResult.Vulnerabilities.Any():
                                    _reporterError.WriteLine(string.Format(
                                        LocalizableStrings.TemplatePackageCoordinator_Download_Error_VulnerablePackage,
                                        packageToInstall).Bold().Red());
                                    DisplayVulnerabilityInfo(installResult.Vulnerabilities, _reporterError);
                                    _reporterError.WriteLine(string.Format(
                                       LocalizableStrings.TemplatePackageCoordinator_Install_Error_VulnerablePackageTip,
                                       packageToInstall,
                                       SharedOptions.ForceOption.Name).Bold());
                                    _reporterError.WriteCommand(Example.For<InstallCommand>(parseResult).WithArgument(BaseInstallCommand.NameArgument, packageToInstall).WithOption(BaseInstallCommand.ForceOption));
                                    break;

                                case UpdateResult updateRequest when updateRequest.Vulnerabilities.Any():
                                    _reporterError.WriteLine(string.Format(
                                        LocalizableStrings.TemplatePackageCoordinator_Update_Error_VulnerablePackage,
                                        packageToInstall).Bold().Red());
                                    DisplayVulnerabilityInfo(updateRequest.Vulnerabilities, _reporterError);
                                    _reporterError.WriteLine(string.Format(
                                        LocalizableStrings.TemplatePackageCoordinator_Update_Error_VulnerablePackageTip,
                                        packageToInstall,
                                        SharedOptions.ForceOption.Name).Bold());
                                    _reporterError.WriteCommand(Example.For<UninstallCommand>(parseResult).WithArgument(BaseUninstallCommand.NameArgument, packageToInstall));
                                    _reporterError.WriteCommand(Example.For<InstallCommand>(parseResult).WithArgument(BaseInstallCommand.NameArgument, packageToInstall).WithOption(BaseInstallCommand.ForceOption));
                                    break;

                                default:
                                    throw new InvalidOperationException($"Unexpected result: {result.GetType()}");
                            }
                        }
                        break;
                    case InstallerErrorCode.GenericError:
                    default:
                        _reporterError.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_lnstall_Error_GenericError,
                                packageToInstall).Bold().Red());
                        break;
                }
            }
        }

        internal async Task DisplayInstalledTemplatePackagesAsync(TemplatePackageManager templatePackageManager, GlobalArgs args, CancellationToken cancellationToken)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<IManagedTemplatePackage> managedTemplatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, cancellationToken).ConfigureAwait(false);

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

                IEnumerable<ITemplateInfo> templates = await templatePackageManager.GetTemplatesAsync(managedSource, cancellationToken).ConfigureAwait(false);
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

        internal void DisplayUpdateCheckResults(IEngineEnvironmentSettings engineEnvironmentSettings, IEnumerable<CheckUpdateResult> versionCheckResults, GlobalArgs args, bool showUpdates = true)
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
                           new TabularOutputSettings(engineEnvironmentSettings.Environment),
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
                    DisplayUpdateCheckErrors(result, ignoreLocalPackageNotFound: false);
                }
                Reporter.Error.WriteLine();
            }
        }

        private void DisplayUpdateCheckErrors(CheckUpdateResult result, bool ignoreLocalPackageNotFound)
        {
            switch (result.Error)
            {
                case InstallerErrorCode.InvalidSource:
                    _reporterError.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_Update_Error_InvalidNuGetFeeds,
                            result.TemplatePackage.DisplayName).Bold().Red());
                    break;
                case InstallerErrorCode.PackageNotFound:
                    if (!ignoreLocalPackageNotFound || !result.TemplatePackage.IsLocalPackage)
                    {
                        _reporterError.WriteLine(
                            string.Format(
                                LocalizableStrings.TemplatePackageCoordinator_Update_Error_PackageNotFound,
                                result.TemplatePackage.DisplayName).Bold().Red());
                    }
                    break;
                case InstallerErrorCode.UnsupportedRequest:
                    _reporterError.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_Update_Error_PackageNotSupported,
                            result.TemplatePackage.DisplayName).Bold().Red());
                    break;
                case InstallerErrorCode.VulnerablePackage when result.Vulnerabilities.Any():
                    {
                        _reporterError.WriteLine(
                                 string.Format(
                                     LocalizableStrings.TemplatePackageCoordinator_UpdateCheck_Error_VulnerablePackage,
                                     result.TemplatePackage.Identifier).Bold().Red());
                        DisplayVulnerabilityInfo(result.Vulnerabilities, _reporterError);
                    }
                    break;
                case InstallerErrorCode.GenericError:
                default:
                    _reporterError.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatePackageCoordinator_Update_Error_GenericError,
                            result.TemplatePackage.DisplayName,
                            result.ErrorMessage).Bold().Red());
                    break;
            }
        }

        private void DisplayVulnerabilityInfo(IReadOnlyList<VulnerabilityInfo> vulnerabilities, IReporter reporter)
        {
            reporter.Write(Environment.NewLine);
            foreach (VulnerabilityInfo entry in vulnerabilities)
            {
                reporter.WriteLine($"{string.Empty.PadLeft(4)}{_vulnerabilitySeverityToColorMap[entry.Severity].Bold()}:");
                reporter.WriteLine(
                    string.Join(
                        Environment.NewLine,
                        entry.AdvisoryUris.Select(advisory => $"{string.Empty.PadLeft(8)}{(advisory.Url(advisory))}")));
                reporter.Write(Environment.NewLine);
            }
        }

        private async Task EvaluateAndDisplayConstraintsAsync(TemplateConstraintManager constraintsManager, IEnumerable<ITemplateInfo> templates, CancellationToken cancellationToken)
        {
            var evaluationResult = await constraintsManager.EvaluateConstraintsAsync(templates, cancellationToken).ConfigureAwait(false);

            var restrictedTemplates = evaluationResult.Where(r => r.Result.Any(cr => cr.EvaluationStatus != TemplateConstraintResult.Status.Allowed));
            if (!restrictedTemplates.Any())
            {
                return;
            }

            _reporterOutput.WriteLine(LocalizableStrings.TemplatePackageCoordinator_Install_ConstraintsNotice);

            foreach (var template in restrictedTemplates)
            {
                bool showIdentity = !string.IsNullOrWhiteSpace(template.Template.GroupIdentity) && templates.Count(t => t.GroupIdentity == template.Template.GroupIdentity) > 1;
                _reporterOutput.WriteLine(template.Template.GetDisplayName(showIdentity: showIdentity));
                foreach (var constraintResult in template.Result.Where(r => r.EvaluationStatus != TemplateConstraintResult.Status.Allowed))
                {
                    _reporterOutput.WriteLine(constraintResult.ToDisplayString().Indent(1));
                }
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
    }
}
