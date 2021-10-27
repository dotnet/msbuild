// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// Resolves the templates to be used for 'dotnet new &lt;name part&gt; --list'.
    /// </summary>
    internal class ListTemplateResolver : BaseTemplateResolver<ListCommandArgs>
    {
        public ListTemplateResolver(TemplatePackageManager templatePackageManager, IHostSpecificDataLoader hostSpecificDataLoader)
            : base(templatePackageManager, hostSpecificDataLoader)
        {
        }

        public ListTemplateResolver(IEnumerable<ITemplateInfo> templateList, IHostSpecificDataLoader hostSpecificDataLoader)
       : base(templateList, hostSpecificDataLoader)
        {
        }

        internal override IEnumerable<CommandParsing.FilterOption> Filters => throw new NotImplementedException();

        internal override async Task<TemplateResolutionResult> ResolveTemplatesAsync(ListCommandArgs args, string? defaultLanguage, CancellationToken cancellationToken)
        {
            IEnumerable<TemplateGroup> templateGroups = await GetTemplateGroupsAsync(cancellationToken).ConfigureAwait(false);
            IEnumerable<Func<TemplateGroup, MatchInfo?>> groupFilters = new[]
            {
                CliFilters.NameTemplateGroupFilter(args.ListNameCriteria)
            };

            IEnumerable<Func<ITemplateInfo, MatchInfo?>> templateFilters =
                args.AppliedFilters
                    .OfType<TemplateFilterOptionDefinition>()
                    .Select(filter => filter.TemplateMatchFilter(args.GetFilterValue(filter)));

            IEnumerable<TemplateGroupMatchInfo> matchInformation =
                templateGroups.Select(
                    group =>
                        TemplateGroupMatchInfo.ApplyFilters(
                            group,
                            groupFilters,
                            templateFilters,
                            CliFilters.EmptyTemplateParameterFilter()));

            return new TemplateResolutionResult(matchInformation, this);
        }
    }
}
