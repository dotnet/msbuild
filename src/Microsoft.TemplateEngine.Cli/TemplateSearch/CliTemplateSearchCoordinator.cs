// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TableOutput;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal static class CliTemplateSearchCoordinator
    {
        /// <summary>
        /// Executes searching for the templates in configured remote sources.
        /// Performs validation for the commands, search for the templates in configured remote source, displays the results in table format.
        /// </summary>
        /// <param name="environmentSettings">environment settings.</param>
        /// <param name="commandInput">new command data.</param>
        /// <param name="defaultLanguage">default language for the host.</param>
        /// <returns><c>New3CommandStatus.Success</c> when the templates were found and displayed;
        /// <c>New3CommandStatus.Cancelled</c> when the command validation fails;
        /// <c>New3CommandStatus.NotFound</c> when no templates found based on the filter criteria.
        /// </returns>
        internal static async Task<New3CommandStatus> SearchForTemplateMatchesAsync(IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, INewCommandInput commandInput, string? defaultLanguage)
        {
            if (!ValidateCommandInput(commandInput))
            {
                return New3CommandStatus.Cancelled;
            }

            Reporter.Output.WriteLine(LocalizableStrings.SearchOnlineNotification);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(environmentSettings, commandInput, defaultLanguage);
            var templatePackages = await templatePackageManager.GetTemplatePackagesAsync(force: false, cancellationToken: default).ConfigureAwait(false);
            SearchResults searchResults = await searchCoordinator.SearchAsync(templatePackages, default).ConfigureAwait(false);

            if (!searchResults.AnySources)
            {
                Reporter.Error.WriteLine(LocalizableStrings.SearchOnlineNoSources.Bold().Red());
                return New3CommandStatus.NotFound;
            }

            if (searchResults.MatchesBySource.Count > 0)
            {
                string? packageIdToShow = null;
                foreach (TemplateSourceSearchResult sourceResult in searchResults.MatchesBySource)
                {
                    DisplayResultsForPack(sourceResult, environmentSettings, commandInput, defaultLanguage);

                    var firstMicrosoftAuthoredPack = sourceResult.PacksWithMatches.FirstOrDefault(p => p.Value.TemplateMatches.Any(t => string.Equals(t.Info.Author, "Microsoft")));
                    if (!firstMicrosoftAuthoredPack.Equals(default(KeyValuePair<PackInfo, TemplatePackSearchResult>)))
                    {
                        packageIdToShow = firstMicrosoftAuthoredPack.Key.Name;
                    }
                }

                Reporter.Output.WriteLine();
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.SearchResultInstallHeader, commandInput.CommandName));
                if (string.IsNullOrWhiteSpace(packageIdToShow))
                {
                    packageIdToShow = searchResults.MatchesBySource[0].PacksWithMatches.First().Key.Name;
                }
                Reporter.Output.WriteLine("Example:");
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.SearchResultInstallCommand, commandInput.CommandName, packageIdToShow));
                return New3CommandStatus.Success;
            }
            else
            {
                string filters = string.Join(", ", SupportedFilterOptions.SupportedSearchFilters.Where(filter => filter.IsFilterSet(commandInput)).Select(filter => $"{filter.Name}='{filter.FilterValue(commandInput)}'"));
                string searchCriteria = string.IsNullOrWhiteSpace(commandInput.TemplateName)
                    ? filters
                    : string.IsNullOrWhiteSpace(filters) ? commandInput.TemplateName : string.Join(", ", commandInput.TemplateName, filters);

                Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoTemplatesMatchingInputParameters, searchCriteria).Bold().Red());
                return New3CommandStatus.NotFound;
            }
        }

        private static void DisplayResultsForPack(TemplateSourceSearchResult sourceResult, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, string? defaultLanguage)
        {
            string sourceHeader = string.Format(LocalizableStrings.SearchResultSourceIndicator, sourceResult.SourceDisplayName);

            Reporter.Output.WriteLine(sourceHeader);
            Reporter.Output.WriteLine();

            IReadOnlyCollection<SearchResultTableRow> data = GetSearchResultsForDisplay(sourceResult, commandInput.Language, defaultLanguage);

            HelpFormatter<SearchResultTableRow> formatter =
                HelpFormatter
                    .For(
                        environmentSettings,
                        commandInput,
                        data,
                        columnPadding: 2,
                        headerSeparator: '-',
                        blankLineBetweenRows: false)
                    .DefineColumn(r => r.TemplateGroupInfo.Name, out object nameColumn, LocalizableStrings.ColumnNameTemplateName, showAlways: true, shrinkIfNeeded: true, minWidth: 15)
                    .DefineColumn(r => r.TemplateGroupInfo.ShortName, LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(r => r.TemplateGroupInfo.Author, LocalizableStrings.ColumnNameAuthor, NewCommandInputCli.AuthorColumnFilter, defaultColumn: true, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(r => r.TemplateGroupInfo.Languages, LocalizableStrings.ColumnNameLanguage, NewCommandInputCli.LanguageColumnFilter, defaultColumn: true)
                    .DefineColumn(r => r.TemplateGroupInfo.Type, LocalizableStrings.ColumnNameType, NewCommandInputCli.TypeColumnFilter, defaultColumn: false)
                    .DefineColumn(r => r.TemplateGroupInfo.Classifications, LocalizableStrings.ColumnNameTags, NewCommandInputCli.TagsColumnFilter, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(r => r.PackageName, out object packageColumn, LocalizableStrings.ColumnNamePackage, showAlways: true)
                    .DefineColumn(r => r.PrintableTotalDownloads, LocalizableStrings.ColumnNameTotalDownloads, showAlways: true, rightAlign: true)
                    .OrderBy(nameColumn, StringComparer.OrdinalIgnoreCase);

            Reporter.Output.WriteLine(formatter.Layout());
        }

        private static IReadOnlyCollection<SearchResultTableRow> GetSearchResultsForDisplay(TemplateSourceSearchResult sourceResult, string language, string? defaultLanguage)
        {
            List<SearchResultTableRow> templateGroupsForDisplay = new List<SearchResultTableRow>();

            foreach (TemplatePackSearchResult packSearchResult in sourceResult.PacksWithMatches.Values)
            {
                var templateGroupsForPack = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(packSearchResult.TemplateMatches.Select(mi => mi.Info), language, defaultLanguage);
                templateGroupsForDisplay.AddRange(templateGroupsForPack.Select(t => new SearchResultTableRow(t, packSearchResult.PackInfo.Name, packSearchResult.PackInfo.TotalDownloads)));
            }

            return templateGroupsForDisplay;
        }

        private static bool ValidateCommandInput(INewCommandInput commandInput)
        {
            if (string.IsNullOrWhiteSpace(commandInput.TemplateName) && SupportedFilterOptions.SupportedSearchFilters.All(filter => !filter.IsFilterSet(commandInput)))
            {
                Reporter.Error.WriteLine(string.Format(
                                            LocalizableStrings.SearchOnlineErrorNoTemplateNameOrFilter,
                                            string.Join(", ", SupportedFilterOptions.SupportedSearchFilters.Select(f => $"'{f.Name}'")),
                                            commandInput.CommandName).Bold().Red());
                return false;
            }

            if (!string.IsNullOrWhiteSpace(commandInput.TemplateName) && commandInput.TemplateName.Length < 2)
            {
                Reporter.Error.WriteLine(LocalizableStrings.SearchOnlineErrorTemplateNameIsTooShort.Bold().Red());
                return false;
            }

            return true;
        }

        /// <summary>
        /// Represents a table row for the template with the package information.
        /// </summary>
        private class SearchResultTableRow
        {
            internal SearchResultTableRow(TemplateGroupTableRow templateGroupTableRow, string packageName, long downloads = 0)
            {
                TemplateGroupInfo = templateGroupTableRow;
                PackageName = packageName;
                TotalDownloads = downloads;
            }

            internal string PackageName { get; private set; }

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
                        return "<1k";
                    }
                    else
                    {
                        return $"{(TotalDownloads / 1000):N0}k";
                    }
                }
            }

            internal TemplateGroupTableRow TemplateGroupInfo { get; private set; }

            internal long TotalDownloads { get; private set; }
        }
    }
}
