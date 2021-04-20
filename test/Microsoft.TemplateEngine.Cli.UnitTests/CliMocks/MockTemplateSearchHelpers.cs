// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal static class MockTemplateSearchHelpers
    {
        private static IReadOnlyList<MatchInfo> defaultMatchInfo = new List<MatchInfo>()
        {
            new MatchInfo(MatchInfo.BuiltIn.Name, "test-name", MatchKind.Exact)
        };

        public static Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> DefaultMatchFilter => (nameMatches) =>
        {
            return nameMatches.Select(match => new TemplateMatchInfo(match.Template, defaultMatchInfo)).ToList();
        };
    }
}
