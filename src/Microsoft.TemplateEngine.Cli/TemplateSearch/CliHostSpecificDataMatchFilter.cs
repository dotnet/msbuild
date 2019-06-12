using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    public class CliHostSpecificDataMatchFilterFactory
    {
        public CliHostSpecificDataMatchFilterFactory(INewCommandInput commandInput, string defaultLanguage)
        {
            _commandInput = commandInput;
            _defaultLanguage = defaultLanguage;
        }

        private readonly INewCommandInput _commandInput;
        private readonly string _defaultLanguage;

        public Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> MatchFilter => (nameMatches) =>
        {
            Dictionary<string, HostSpecificTemplateData> hostDataLookup = new Dictionary<string, HostSpecificTemplateData>();

            foreach (ITemplateNameSearchResult result in nameMatches)
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

            TemplateListResolutionResult templateResolutionResult = TemplateListResolver.GetTemplateResolutionResult(nameMatches.Select(x => x.Template).ToList(), hostSpecificDataLoader, _commandInput, _defaultLanguage);

            IReadOnlyList<ITemplateMatchInfo> templateMatches;
            if (templateResolutionResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousMatches))
            {
                templateMatches = unambiguousMatches;
            }
            else
            {
                templateMatches = templateResolutionResult.GetBestTemplateMatchList();
            }

            return templateMatches;
        };
    }
}
