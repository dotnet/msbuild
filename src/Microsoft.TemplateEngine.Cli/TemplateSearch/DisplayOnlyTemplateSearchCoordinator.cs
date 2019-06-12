using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class DisplayOnlyTemplateSearchCoordinator : TemplateSearchCoordinator
    {
        public DisplayOnlyTemplateSearchCoordinator(IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, string defaultLanguage)
            : base(environmentSettings, commandInput.TemplateName, defaultLanguage)
        {
            _commandInput = commandInput;
        }

        protected readonly INewCommandInput _commandInput;

        protected override Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> MatchFilter => new CliHostSpecificDataMatchFilterFactory(_commandInput, _defaultLanguage).MatchFilter;

        protected override bool HandleSearchResults()
        {
            if (_searchResults.AnySources)
            {
                // Only show the searching online message if there are sources to search.
                // It's a bit out of order to do the search first, then display the message.
                // But there's no way to know whether or not there are sources without searching.
                // ...theoretically the search source initialization is separate from the search, but the initialization is most of the work.
                Reporter.Output.WriteLine(LocalizableStrings.SearchingOnlineNotification.Bold().Red());
            }
            else
            {
                return false;
            }

            bool anyMatches = false;

            foreach (TemplateSourceSearchResult sourceResult in _searchResults.MatchesBySource)
            {
                if (sourceResult.PacksWithMatches.Values.Any(match => match.TemplateMatches.Any(t => t.IsInvokableMatch())))
                {
                    string sourceHeader = string.Format(LocalizableStrings.SearchResultSourceIndicator, sourceResult.SourceDisplayName);

                    Reporter.Output.WriteLine(sourceHeader);
                    Reporter.Output.WriteLine(new string('-', sourceHeader.Length));

                    foreach (TemplatePackSearchResult matchesForPack in sourceResult.PacksWithMatches.Values)
                    {
                        DisplayResultsForPack(matchesForPack);
                        Reporter.Output.WriteLine();
                    }

                    anyMatches = true;
                }
            }

            if (!anyMatches)
            {
                Reporter.Output.WriteLine(LocalizableStrings.SearchResultNoMatches.Bold().Red());
            }

            return anyMatches;
        }

        private void DisplayResultsForPack(TemplatePackSearchResult matchesForPack)
        {
            bool firstResult = true;

            foreach (ITemplateMatchInfo templateMatch in matchesForPack.TemplateMatches)
            {
                if (!firstResult)
                {
                    Reporter.Output.WriteLine();
                }
                firstResult = false;

                // TODO: get the Pack authoring info plumbed through - this will require changes to the scraper output
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.SearchResultPackInfo, templateMatch.Info.Name, templateMatch.Info.ShortName, templateMatch.Info.Author, matchesForPack.PackInfo.Name));
                Reporter.Output.WriteLine(LocalizableStrings.SearchResultInstallHeader);
                string fullyQualifiedPackName = $"{matchesForPack.PackInfo.Name}::{matchesForPack.PackInfo.Version}";
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.SearchResultInstallCommand, _commandInput.CommandName, fullyQualifiedPackName));
            }
        }
    }
}
