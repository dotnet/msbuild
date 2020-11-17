using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Template;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Provides the collection of supported filters for different command options. At the moment only --list and --search options are supported.
    /// </summary>
    /// <remarks>
    /// Intention of SupportedFilterOptions and FilterOption classes is to make the filters extendable. In case new filter option is implemented for dotnet new command, it should be enough to:
    /// - implement corresponding FilterOption class
    /// - add filter to required collection in SupportedFilterOptions class to apply it to certain dotnet new --option
    /// Currenty supported action options and their filter options:
    /// - --list|-l: --author, --language, --type, --baseline
    /// - --search: --author, --language, --type, --baseline, --package
    /// Potentially the approach should be extended to --help and template instatiation actions.
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
                TypeFilter
            };

            SupportedSearchFilters = new List<FilterOption>()
            {
                AuthorFilter,
                BaselineFilter,
                LanguageFilter,
                TypeFilter,
                PackageFilter
            };
        }

        /// <summary>
        /// Supported filters for --list option
        /// </summary>
        internal static IReadOnlyCollection<FilterOption> SupportedListFilters { get; private set; }

        /// <summary>
        /// Supported filters for --search option
        /// </summary>
        internal static IReadOnlyCollection<FilterOption> SupportedSearchFilters { get; private set; }

        internal static FilterOption AuthorFilter { get; } = new TemplateFilterOption()
        {
            Name = "author",
            FilterValue = command => command.AuthorFilter,
            IsFilterSet = command => !string.IsNullOrWhiteSpace(command.AuthorFilter),
            TemplateMatchFilter = command => WellKnownSearchFilters.AuthorFilter(command.AuthorFilter),
            MismatchCriteria = resolutionResult => resolutionResult.HasAuthorMismatch
        };

        internal static FilterOption BaselineFilter { get; } = new TemplateFilterOption()
        {
            Name = "baseline",
            FilterValue = command => command.BaselineName,
            IsFilterSet = command => !string.IsNullOrWhiteSpace(command.BaselineName),
            TemplateMatchFilter = command => WellKnownSearchFilters.BaselineFilter(command.BaselineName),
            MismatchCriteria = resolutionResult => resolutionResult.HasBaselineMismatch
        };

        internal static FilterOption LanguageFilter { get; } = new TemplateFilterOption()
        {
            Name = "language",
            FilterValue = command => command.Language,
            IsFilterSet = command => !string.IsNullOrWhiteSpace(command.Language),
            TemplateMatchFilter = command => WellKnownSearchFilters.LanguageFilter(command.Language),
            MismatchCriteria = resolutionResult => resolutionResult.HasLanguageMismatch
        };

        internal static FilterOption TypeFilter { get; } = new TemplateFilterOption()
        {
            Name = "type",
            FilterValue = command => command.TypeFilter,
            IsFilterSet = command => !string.IsNullOrWhiteSpace(command.TypeFilter),
            TemplateMatchFilter = command => WellKnownSearchFilters.ContextFilter(command.TypeFilter),
            MismatchCriteria = resolutionResult => resolutionResult.HasContextMismatch
        };

        internal static FilterOption PackageFilter { get; } = new PackageFilterOption()
        {
            Name = "package",
            FilterValue = command => command.PackageFilter,
            IsFilterSet = command => !string.IsNullOrWhiteSpace(command.PackageFilter),
            PackageMatchFilter = command =>
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
        };
    }
}
