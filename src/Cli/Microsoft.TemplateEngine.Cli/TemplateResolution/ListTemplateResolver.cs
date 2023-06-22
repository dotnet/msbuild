// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// Resolves the templates to be used for 'dotnet new &lt;name part&gt; --list'.
    /// </summary>
    internal class ListTemplateResolver : BaseTemplateResolver<ListCommandArgs>
    {
        private readonly TemplateConstraintManager? _constraintManager;

        public ListTemplateResolver(TemplateConstraintManager constraintManager, TemplatePackageManager templatePackageManager, IHostSpecificDataLoader hostSpecificDataLoader)
            : base(templatePackageManager, hostSpecificDataLoader)
        {
            _constraintManager = constraintManager;
        }

        public ListTemplateResolver(IEnumerable<ITemplateInfo> templateList, IHostSpecificDataLoader hostSpecificDataLoader, TemplateConstraintManager? constraintManager = null)
       : base(templateList, hostSpecificDataLoader)
        {
            _constraintManager = constraintManager;
        }

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

            if (!args.IgnoreConstraints && _constraintManager is not null)
            {
                templateFilters = templateFilters.Concat(WellKnownSearchFilters.ConstraintFilters(await _constraintManager.GetConstraintsAsync(templateGroups.SelectMany(tg => tg.Templates).Cast<ITemplateInfo>(), cancellationToken).ConfigureAwait(false)));
            }

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
