using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateSearch.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal static class MockTemplateSearchHelpers
    {
        private static IReadOnlyList<MatchInfo> DefaultMatchInfo = new List<MatchInfo>()
        {
            new MatchInfo()
            {
                Location = MatchLocation.Name,
                Kind = MatchKind.Exact
            }
        };

        public static Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> DefaultMatchFilter => (nameMatches) =>
        {
            return nameMatches.Select(match => new TemplateMatchInfo(match.Template, DefaultMatchInfo)).ToList();
        };
    }
}
