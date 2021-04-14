// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Provides the collection of supported filters for different command options. At the moment only --list and --search options are supported.
    /// </summary>
    /// <remarks>
    /// Intention of SupportedFilterOptions and FilterOption classes is to make the filters extendable. In case new filter option is implemented for dotnet new command, it should be enough to:
    /// - implement corresponding FilterOption class
    /// - add filter to required collection in SupportedFilterOptions class to apply it to certain dotnet new --option
    /// Currently supported action options and their filter options:
    /// - --list|-l: --author, --language, --type, --baseline, --tag
    /// - --search: --author, --language, --type, --baseline, --package, --tag
    /// Potentially the approach should be extended to --help and template instantiation actions.
    /// </remarks>
    internal static class SupportedFilterOptions
    {
        static SupportedFilterOptions()
        {
            SupportedListFilters = new List<FilterOption>()
            {
                AuthorFilter,
                BaselineFilter,
                LanguageFilter,
                TypeFilter,
                TagFilter
            };

            SupportedSearchFilters = new List<FilterOption>()
            {
                AuthorFilter,
                BaselineFilter,
                LanguageFilter,
                TypeFilter,
                PackageFilter,
                TagFilter
            };
        }

        /// <summary>
        /// Supported filters for --list option.
        /// </summary>
        internal static IReadOnlyCollection<FilterOption> SupportedListFilters { get; private set; }

        /// <summary>
        /// Supported filters for --search option.
        /// </summary>
        internal static IReadOnlyCollection<FilterOption> SupportedSearchFilters { get; private set; }

        internal static FilterOption AuthorFilter { get; } =
            new TemplateFilterOption(
                "author",
                filterValue: command => command.AuthorFilter,
                isFilterSet: command => !string.IsNullOrWhiteSpace(command.AuthorFilter),
                matchFilter: command => WellKnownSearchFilters.AuthorFilter(command.AuthorFilter),
                mismatchCriteria: resolutionResult => resolutionResult.HasAuthorMismatch);

        internal static FilterOption BaselineFilter { get; } =
            new TemplateFilterOption(
                "baseline",
                filterValue: command => command.BaselineName,
                isFilterSet: command => !string.IsNullOrWhiteSpace(command.BaselineName),
                matchFilter: command => WellKnownSearchFilters.BaselineFilter(command.BaselineName),
                mismatchCriteria: resolutionResult => resolutionResult.HasBaselineMismatch);

        internal static FilterOption LanguageFilter { get; } =
            new TemplateFilterOption(
                "language",
                filterValue: command => command.Language,
                isFilterSet: command => !string.IsNullOrWhiteSpace(command.Language),
                matchFilter: command => WellKnownSearchFilters.LanguageFilter(command.Language),
                mismatchCriteria: resolutionResult => resolutionResult.HasLanguageMismatch);

        internal static FilterOption TagFilter { get; } =
            new TemplateFilterOption(
                "tag",
                filterValue: command => command.TagFilter,
                isFilterSet: command => !string.IsNullOrWhiteSpace(command.TagFilter),
                matchFilter: command => WellKnownSearchFilters.ClassificationFilter(command.TagFilter),
                mismatchCriteria: resolutionResult => resolutionResult.HasClassificationMismatch);

        internal static FilterOption TypeFilter { get; } =
            new TemplateFilterOption(
                "type",
                filterValue: command => command.TypeFilter,
                isFilterSet: command => !string.IsNullOrWhiteSpace(command.TypeFilter),
                matchFilter: command => WellKnownSearchFilters.TypeFilter(command.TypeFilter),
                mismatchCriteria: resolutionResult => resolutionResult.HasTypeMismatch);

        internal static FilterOption PackageFilter { get; } =
            new PackageFilterOption(
                "package",
                filterValue: command => command.PackageFilter,
                isFilterSet: command => !string.IsNullOrWhiteSpace(command.PackageFilter),
                matchFilter: PackageMatchFilter);

        private static Func<PackInfo, bool> PackageMatchFilter(INewCommandInput command)
        {
            return (pack) =>
            {
                if (string.IsNullOrWhiteSpace(command.PackageFilter))
                {
                    return true;
                }
                return pack.Name.IndexOf(command.PackageFilter, StringComparison.OrdinalIgnoreCase) > -1;
            };
        }
    }
}
