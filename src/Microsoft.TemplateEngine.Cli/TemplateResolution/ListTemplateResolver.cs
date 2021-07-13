// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// Resolves the templates to be used for 'dotnet new &lt;name part&gt; --list'.
    /// </summary>
    internal class ListTemplateResolver : BaseTemplateResolver
    {
        private static IReadOnlyList<FilterOption> _supportedFilters = new[]
        {
            SupportedFilterOptions.AuthorFilter,
            SupportedFilterOptions.BaselineFilter,
            SupportedFilterOptions.LanguageFilter,
            SupportedFilterOptions.TypeFilter,
            SupportedFilterOptions.TagFilter
        };

        public ListTemplateResolver(TemplatePackageManager templatePackageManager, IHostSpecificDataLoader hostSpecificDataLoader)
            : base(templatePackageManager, hostSpecificDataLoader)
        {
        }

        public ListTemplateResolver(IEnumerable<ITemplateInfo> templateList, IHostSpecificDataLoader hostSpecificDataLoader)
       : base(templateList, hostSpecificDataLoader)
        {
        }

        internal static IEnumerable<FilterOption> SupportedFilters => _supportedFilters;

        internal override IEnumerable<FilterOption> Filters => SupportedFilters;

        internal override async Task<TemplateResolutionResult> ResolveTemplatesAsync(INewCommandInput commandInput, string? defaultLanguage, CancellationToken cancellationToken)
        {
            IEnumerable<TemplateGroup> templateGroups = await GetTemplateGroupsAsync(cancellationToken).ConfigureAwait(false);

            IEnumerable<Func<TemplateGroup, MatchInfo?>> groupFilters = new[]
            {
                CliFilters.NameTemplateGroupFilter(commandInput.TemplateName)
            };

            IEnumerable<Func<ITemplateInfo, MatchInfo?>> templateFilters =
                SupportedFilters
                    .OfType<TemplateFilterOption>()
                    .Select(filter => filter.TemplateMatchFilter(commandInput));

            IEnumerable<TemplateGroupMatchInfo> matchInformation =
                templateGroups.Select(
                    group =>
                        TemplateGroupMatchInfo.ApplyFilters(
                            group,
                            groupFilters,
                            templateFilters,
                            CliFilters.ListTemplateParameterFilter(HostSpecificDataLoader, commandInput)));

            return new TemplateResolutionResult(matchInformation, this);
        }
    }
}
