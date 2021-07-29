// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliHostSpecificDataMatchFilterFactory
    {
        private readonly INewCommandInput _commandInput;

        internal CliHostSpecificDataMatchFilterFactory(INewCommandInput commandInput)
        {
            _commandInput = commandInput;
        }

        internal Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> MatchFilter => (foundPackages) =>
        {
            Dictionary<string, HostSpecificTemplateData> hostDataLookup = new Dictionary<string, HostSpecificTemplateData>();
            foreach (ITemplateNameSearchResult result in foundPackages)
            {
                if (result is CliTemplateNameSearchResult cliResult)
                {
                    hostDataLookup[cliResult.Template.Identity] = cliResult.HostSpecificTemplateData;
                }
                else
                {
                    hostDataLookup[result.Template.Identity] = HostSpecificTemplateData.Default;
                }
            }
            IHostSpecificDataLoader hostSpecificDataLoader = new InMemoryHostSpecificDataLoader(hostDataLookup);

            IEnumerable<Func<INewCommandInput, Func<PackInfo, bool>>> packageFiltersToUse = CliTemplateSearchCoordinator.SupportedFilters
                                    .OfType<PackageFilterOption>()
                                    .Select(filter => filter.PackageMatchFilter);

            IEnumerable<ITemplateNameSearchResult> templatesToFilter =
                foundPackages.Where(foundPackage => packageFiltersToUse.All(pf => pf(_commandInput)(foundPackage.PackInfo)));

            IEnumerable<ITemplateInfo> templates = templatesToFilter.Select(x => x.Template).Where(template => !template.IsHiddenByHostFile(hostSpecificDataLoader));
            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(templates);
            IEnumerable<Func<TemplateGroup, MatchInfo?>> groupFilters = new[]
            {
                CliFilters.NameTemplateGroupFilter(_commandInput.SearchNameCriteria)
            };

            IEnumerable<Func<ITemplateInfo, MatchInfo?>> templateFilters =
                CliTemplateSearchCoordinator.SupportedFilters
                    .OfType<TemplateFilterOption>()
                    .Select(filter => filter.TemplateMatchFilter(_commandInput));

            IEnumerable<TemplateGroupMatchInfo> matchInformation =
                templateGroups.Select(
                    group =>
                        TemplateGroupMatchInfo.ApplyFilters(
                            group,
                            groupFilters,
                            templateFilters,
                            CliFilters.ListTemplateParameterFilter(hostSpecificDataLoader, _commandInput)));
            return matchInformation.Where(group => group.IsGroupAndTemplateInfoAndParametersMatch).SelectMany(group => group.TemplateMatchInfosWithMatchingParameters).ToList();
        };
    }
}
