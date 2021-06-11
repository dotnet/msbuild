// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal static class TemplateMatchInfoExtensions
    {
        internal static bool HasDefaultLanguageMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Name == TemplateResolver.DefaultLanguageMatchParameterName && x.Kind == MatchKind.Exact);
        }

        internal static bool IsInvokableMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Count > 0
                            && templateMatchInfo.MatchDisposition.All(x => x.Kind == MatchKind.Exact);
        }

        internal static bool IsFilterableMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Count > 0
                            && templateMatchInfo.MatchDisposition.All(x =>
                                x.Kind == MatchKind.Exact
                                ||
                                    // these locations can have partial or exact matches.
                                    x.Kind == MatchKind.Partial
                                    && (x.Name == MatchInfo.BuiltIn.Name
                                        || x.Name == MatchInfo.BuiltIn.ShortName
                                        || x.Name == MatchInfo.BuiltIn.Author));
        }

        internal static bool HasInvalidParameterName(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.OfType<ParameterMatchInfo>().Any(x => x.Kind == MatchKind.InvalidName);
        }

        internal static IEnumerable<string> GetInvalidParameterNames(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition
                .OfType<ParameterMatchInfo>()
                .Where(match => match.Kind == MatchKind.InvalidName)
                .Select(match => match.Name);
        }

        // This is analogous to INewCommandInput.InputTemplateParams
        internal static IReadOnlyDictionary<string, string?> GetValidTemplateParameters(this ITemplateMatchInfo templateMatchInfo)
        {
            //the method should be revised as valid parameters should be taken from command and not from match dispositionS
            return templateMatchInfo.MatchDisposition
                .OfType<ParameterMatchInfo>()
                .Where(x => x.Kind == MatchKind.Exact)
                .ToDictionary(x => x.Name, x => x.Value);
        }
    }
}
