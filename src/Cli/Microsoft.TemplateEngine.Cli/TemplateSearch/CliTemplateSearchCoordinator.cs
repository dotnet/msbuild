// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.Common.Abstractions;
using static Microsoft.TemplateEngine.Cli.NuGet.NugetApiManager;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal static class CliTemplateSearchCoordinator
    {
        /// <summary>
        /// Executes searching for the templates in configured remote sources.
        /// Performs validation for the commands, search for the templates in configured remote source, displays the results in table format.
        /// </summary>
        /// <param name="environmentSettings">environment settings.</param>
        /// <param name="templatePackageManager"></param>
        /// <param name="commandArgs">new command data.</param>
        /// <param name="defaultLanguage">default language for the host.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="NewCommandStatus.Success"/> when the templates were found and displayed;
        /// <see cref="NewCommandStatus.MissingRequiredOption"/> when the command validation fails;
        /// <see cref="NewCommandStatus.NotFound"/> when no templates found based on the filter criteria.
        /// </returns>
        internal static async Task<NewCommandStatus> SearchForTemplateMatchesAsync(
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            SearchCommandArgs commandArgs,
            string? defaultLanguage,
            CancellationToken cancellationToken)
        {
            if (!ValidateCommandInput(commandArgs))
            {
                return NewCommandStatus.MissingRequiredOption;
            }

            Reporter.Output.WriteLine(LocalizableStrings.CliTemplateSearchCoordinator_Info_SearchInProgress);
            IReadOnlyList<IManagedTemplatePackage> templatePackages =
                await templatePackageManager.GetManagedTemplatePackagesAsync(force: false, cancellationToken: cancellationToken).ConfigureAwait(false);

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(environmentSettings);
            CliSearchFiltersFactory searchFiltersFactory = new(templatePackages);

            IReadOnlyList<SearchResult>? searchResults = await searchCoordinator.SearchAsync(
                searchFiltersFactory.GetPackFilter(commandArgs),
                CliSearchFiltersFactory.GetMatchingTemplatesFilter(commandArgs),
                cancellationToken).ConfigureAwait(false);

            if (!searchResults.Any())
            {
                Reporter.Error.WriteLine(LocalizableStrings.CliTemplateSearchCoordinator_Error_NoSources.Bold().Red());
                return NewCommandStatus.NotFound;
            }

            foreach (SearchResult result in searchResults)
            {
                if (!result.Success)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.CliTemplateSearchCoordinator_Info_MatchesFromSource, result.Provider.Factory.DisplayName);
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.CliTemplateSearchCoordinator_Error_SearchFailure, result.ErrorMessage).Red().Bold());
                    continue;
                }

                Reporter.Output.WriteLine(LocalizableStrings.CliTemplateSearchCoordinator_Info_MatchesFromSource, result.Provider.Factory.DisplayName);
                if (result.SearchHits.Any())
                {
                    DisplayResultsForPack(result.SearchHits, environmentSettings, commandArgs, defaultLanguage);
                }
                else
                {
                    //TODO: implement it for template options matching
                    //IReadOnlyDictionary<string, string?>? appliedParameterMatches = TemplateCommandInput.GetTemplateParametersFromCommand(commandArgs);
                    // No templates found matching the following input parameter(s): {0}.
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.Generic_Info_NoMatchingTemplates,
                            GetInputParametersString(commandArgs))
                        .Bold().Red());
                }
            }
            Reporter.Output.WriteLine();
            if (searchResults.Where(r => r.Success).SelectMany(r => r.SearchHits).Any())
            {
                string packageIdToShow = EvaluatePackageToShow(searchResults);
                Reporter.Output.WriteLine(LocalizableStrings.CliTemplateSearchCoordinator_Info_InstallHelp);
                Reporter.Output.WriteCommand(
                 Example
                     .For<NewCommand>(commandArgs.ParseResult)
                     .WithSubcommand<InstallCommand>()
                     .WithArgument(BaseInstallCommand.NameArgument));
                Reporter.Output.WriteLine(LocalizableStrings.Generic_ExampleHeader);
                Reporter.Output.WriteCommand(
                   Example
                       .For<NewCommand>(commandArgs.ParseResult)
                       .WithSubcommand<InstallCommand>()
                       .WithArgument(BaseInstallCommand.NameArgument, packageIdToShow));
                return NewCommandStatus.Success;
            }
            return NewCommandStatus.NotFound;
        }

        internal static async Task<(NugetPackageMetadata?, IReadOnlyList<ITemplateInfo>)> SearchForPackageDetailsAsync(
            IEngineEnvironmentSettings environmentSettings,
            NugetApiManager nugetApiManager,
            string packageIdentifier,
            string? version,
            CancellationToken cancellationToken)
        {
            var nugetPackage = await nugetApiManager.GetPackageMetadataAsync(packageIdentifier, version, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (nugetPackage != null)
            {
                var packages = await SearchForPackageTemplatesAsync(
                    environmentSettings,
                    packageIdentifier,
                    version,
                    cancellationToken).ConfigureAwait(false);
                return (nugetPackage, packages);
            }

            return (null, new List<ITemplateInfo>());
        }

        internal static async Task<IReadOnlyList<ITemplateInfo>> SearchForPackageTemplatesAsync(
            IEngineEnvironmentSettings environmentSettings,
            string packageIdentifier,
            string? version,
            CancellationToken cancellationToken)
        {
            var searchResults = await CliTemplateSearchCoordinatorFactory
                    .CreateCliTemplateSearchCoordinator(environmentSettings)
                    .SearchAsync(
                        f => f.Name == packageIdentifier && (string.IsNullOrEmpty(version) || f.Version == version),
                        t => t.Templates,
                        cancellationToken).ConfigureAwait(false);

            if (searchResults.Any() && searchResults[0].SearchHits.Any())
            {
                return searchResults[0].SearchHits[0].MatchedTemplates;
            }

            return new List<ITemplateInfo>();
        }

        private static string EvaluatePackageToShow(IReadOnlyList<SearchResult> searchResults)
        {
            var microsoftAuthoredPackages = searchResults
                .SelectMany(r => r.SearchHits)
                .Where(hit => hit.PackageInfo.Name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                                && hit.MatchedTemplates.Any(t => t.Author == "Microsoft"))
                .OrderByDescending(hit => hit.PackageInfo.TotalDownloads);

            if (microsoftAuthoredPackages.Any())
            {
                return microsoftAuthoredPackages.First().PackageInfo.Name;
            }
            else
            {
                return searchResults
                        .SelectMany(r => r.SearchHits)
                        .OrderByDescending(hit => hit.PackageInfo.TotalDownloads)
                        .First().PackageInfo.Name;
            }
        }

        private static void DisplayResultsForPack(
            IReadOnlyList<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)> results,
            IEngineEnvironmentSettings environmentSettings,
            SearchCommandArgs commandArgs,
            string? defaultLanguage)
        {
            //TODO: implement it for template options matching
            //IReadOnlyDictionary<string, string?>? appliedParameterMatches = TemplateCommandInput.GetTemplateParametersFromCommand(commandArgs);
            Reporter.Output.WriteLine(LocalizableStrings.TemplatesFoundMatchingInputParameters, GetInputParametersString(commandArgs));
            Reporter.Output.WriteLine();
            IReadOnlyCollection<SearchResultTableRow> data = GetSearchResultsForDisplay(results, commandArgs.Language, defaultLanguage, environmentSettings.Environment);

            TabularOutput<SearchResultTableRow> formatter =
               TabularOutput.TabularOutput
                    .For(
                        new TabularOutputSettings(environmentSettings.Environment, commandArgs),
                        data
                          .OrderByDescending(d => d.TotalDownloads, SearchResultTableRow.TotalDownloadsComparer)
                          .ThenBy(d => d.TemplateGroupInfo.Name, StringComparer.CurrentCultureIgnoreCase))
                    .DefineColumn(r => r.TemplateGroupInfo.Name, out object? nameColumn, LocalizableStrings.ColumnNameTemplateName, showAlways: true, shrinkIfNeeded: true, minWidth: 15)
                    .DefineColumn(r => r.TemplateGroupInfo.ShortNames, LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(r => r.TemplateGroupInfo.Author, LocalizableStrings.ColumnNameAuthor, TabularOutputSettings.ColumnNames.Author, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(r => r.TemplateGroupInfo.Languages, LocalizableStrings.ColumnNameLanguage, TabularOutputSettings.ColumnNames.Language, defaultColumn: true)
                    .DefineColumn(r => r.TemplateGroupInfo.Type, LocalizableStrings.ColumnNameType, TabularOutputSettings.ColumnNames.Type, defaultColumn: false)
                    .DefineColumn(r => r.TemplateGroupInfo.Classifications, LocalizableStrings.ColumnNameTags, TabularOutputSettings.ColumnNames.Tags, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(r => GetPackageInfo(r.PackageName, r.PackageOwners), out object? packageColumn, LocalizableStrings.ColumnNamePackageNameAndOwners, showAlways: true)
                    .DefineColumn(r => GetReservedMark(r.Reserved), LocalizableStrings.ColumnNameTrusted, showAlways: true, textAlign: TextAlign.Center)
                    .DefineColumn(r => r.PrintableTotalDownloads, out object? downloadsColumn, LocalizableStrings.ColumnNameTotalDownloads, showAlways: true, textAlign: TextAlign.Center);

            Reporter.Output.WriteLine(formatter.Layout());
        }

        private static string GetReservedMark(bool reserved) => reserved ? "✓" : string.Empty;

        private static string GetPackageInfo(string packageName, string packageOwners)
        {
            // take max number of package id symbols https://learn.microsoft.com/en-us/nuget/reference/nuspec because this value has to be fully displayed
            var maxColumnWidth = 128;
            var packageIdLength = packageName.Length;

            var formattedOutput = packageName;
            var maxPackageOwnerLength = maxColumnWidth - packageIdLength;

            if (maxPackageOwnerLength > 0)
            {
                formattedOutput = packageOwners.Length > maxPackageOwnerLength
                    ? $"{packageName} / {packageOwners.Substring(0, maxPackageOwnerLength)}"
                    : $"{packageName} / {packageOwners}";
            }

            return formattedOutput;
        }

        private static IReadOnlyCollection<SearchResultTableRow> GetSearchResultsForDisplay(
            IReadOnlyList<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)> results,
            string? language,
            string? defaultLanguage,
            IEnvironment environment)
        {
            List<SearchResultTableRow> templateGroupsForDisplay = new();

            foreach (var packSearchResult in results)
            {
                var templateGroupsForPack = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(packSearchResult.MatchedTemplates, language, defaultLanguage, environment);
                templateGroupsForDisplay.AddRange(templateGroupsForPack.Select(t =>
                new SearchResultTableRow(
                    t,
                    packSearchResult.PackageInfo.Name,
                    string.Join(", ", packSearchResult.PackageInfo.Owners),
                    packSearchResult.PackageInfo.Reserved,
                    packSearchResult.PackageInfo.TotalDownloads)));
            }

            return templateGroupsForDisplay;
        }

        private static bool ValidateCommandInput(SearchCommandArgs commandArgs)
        {
            if (string.IsNullOrWhiteSpace(commandArgs.SearchNameCriteria) && !commandArgs.AppliedFilters.Any())
            //TODO: implement it for template options matching
            // && !commandInput.RemainingParameters.Any())
            {
                Reporter.Error.WriteLine(LocalizableStrings.CliTemplateSearchCoordinator_Error_NoTemplateName.Red().Bold());
                Reporter.Error.WriteLine(LocalizableStrings.CliTemplateSearchCoordinator_Info_SearchHelp, string.Join(", ", BaseSearchCommand.SupportedFilters.Select(f => $"'{f.OptionFactory().Name}'")));
                Reporter.Error.WriteLine(LocalizableStrings.Generic_ExamplesHeader);
                Reporter.Error.WriteCommand(
                    Example
                        .For<NewCommand>(commandArgs.ParseResult)
                        .WithSubcommand<SearchCommand>()
                        .WithArgument(BaseSearchCommand.NameArgument, "web"));

                Reporter.Error.WriteCommand(
                     Example
                        .For<NewCommand>(commandArgs.ParseResult)
                        .WithSubcommand<SearchCommand>()
                        .WithOption(SharedOptionsFactory.CreateAuthorOption(), "Microsoft"));

                Reporter.Error.WriteCommand(
                 Example
                    .For<NewCommand>(commandArgs.ParseResult)
                    .WithSubcommand<SearchCommand>()
                    .WithArgument(BaseSearchCommand.NameArgument, "web")
                    .WithOption(SharedOptionsFactory.CreateLanguageOption(), "C#"));

                return false;
            }

            if (!string.IsNullOrWhiteSpace(commandArgs.SearchNameCriteria) && commandArgs.SearchNameCriteria.Length < 2)
            {
                Reporter.Error.WriteLine(LocalizableStrings.CliTemplateSearchCoordinator_Error_TemplateNameIsTooShort.Bold().Red());
                return false;
            }

            return true;
        }

        private static string GetInputParametersString(SearchCommandArgs commandArgs/*, IReadOnlyDictionary<string, string?>? templateParameters = null*/)
        {
            string separator = ", ";
            IEnumerable<string> appliedFilters = commandArgs.AppliedFilters
                .Select(filter => $"{commandArgs.GetFilterToken(filter)}='{commandArgs.GetFilterValue(filter)}'");

            //TODO: implement it for template options matching
            //IEnumerable<string> appliedTemplateParameters = templateParameters?
            //       .Select(param => string.IsNullOrWhiteSpace(param.Value) ? param.Key : $"{param.Key}='{param.Value}'") ?? Array.Empty<string>();

            StringBuilder inputParameters = new();
            string? mainCriteria = commandArgs.SearchNameCriteria;
            if (!string.IsNullOrWhiteSpace(mainCriteria))
            {
                inputParameters.Append($"'{mainCriteria}'");
                if (appliedFilters.Any()/* || appliedTemplateParameters.Any()*/)
                {
                    inputParameters.Append(separator);
                }
            }
            if (appliedFilters/*.Concat(appliedTemplateParameters)*/.Any())
            {
                inputParameters.Append(string.Join(separator, appliedFilters/*.Concat(appliedTemplateParameters)*/));
            }
            return inputParameters.ToString();
        }

        /// <summary>
        /// Represents a table row for the template with the package information.
        /// </summary>
        internal class SearchResultTableRow
        {
            private const string MinimumDownloadCount = "<1k";
            private const char ThousandsChar = 'k';

            internal SearchResultTableRow(TemplateGroupTableRow templateGroupTableRow, string packageName, string packageOwners, bool reserved, long downloads = 0)
            {
                TemplateGroupInfo = templateGroupTableRow;
                PackageName = packageName;
                PackageOwners = packageOwners;
                TotalDownloads = downloads;
                Reserved = reserved;
            }

            internal static IComparer<long> TotalDownloadsComparer { get; } = new ThousandComparer();

            internal string PackageName { get; private set; }

            internal string PackageOwners { get; private set; }

            internal bool Reserved { get; private set; }

            internal string PrintableTotalDownloads
            {
                get
                {
                    if (TotalDownloads < 1)
                    {
                        return string.Empty;
                    }
                    else if (TotalDownloads < 1000)
                    {
                        return MinimumDownloadCount;
                    }
                    else
                    {
                        return $"{(TotalDownloads / 1000):N0}{ThousandsChar}";
                    }
                }
            }

            internal TemplateGroupTableRow TemplateGroupInfo { get; private set; }

            internal long TotalDownloads { get; private set; }

            private class ThousandComparer : IComparer<long>
            {
                public int Compare(long x, long y)
                {
                    if (x == y || x < 1 && y < 1)
                    {
                        return 0;
                    }
                    if (x < 1)
                    {
                        return -1;
                    }
                    if (y < 1)
                    {
                        return 1;
                    }
                    return (x / 1000).CompareTo(y / 1000);
                }
            }
        }
    }
}


