using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    public class CliHostSpecificDataMatchFilterFactory
    {
        public CliHostSpecificDataMatchFilterFactory(INewCommandInput commandInput)
        {
            _commandInput = commandInput;
        }

        private readonly INewCommandInput _commandInput;

        public Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> MatchFilter => (foundPackages) =>
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

            IEnumerable<Func<INewCommandInput, Func<PackInfo, bool>>> packageFiltersToUse = SupportedFilterOptions.SupportedSearchFilters
                                    .OfType<PackageFilterOption>()
                                    .Select(filter => filter.PackageMatchFilter);

            IEnumerable<ITemplateNameSearchResult> templatesToFilter =
                foundPackages.Where(foundPackage => packageFiltersToUse.All(pf => pf(_commandInput)(foundPackage.PackInfo)));

            return TemplateResolver.PerformCoreTemplateQueryForSearch(templatesToFilter.Select(x => x.Template), hostSpecificDataLoader, _commandInput).ToList();
        };
    }
}
