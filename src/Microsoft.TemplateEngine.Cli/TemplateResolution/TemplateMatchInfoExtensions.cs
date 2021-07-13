// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal static class TemplateMatchInfoExtensions
    {
        internal static IReadOnlyDictionary<string, string?> GetValidTemplateParameters(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition
                .OfType<ParameterMatchInfo>()
                .Where(x => x.Kind == MatchKind.Exact)
                .ToDictionary(x => x.Name, x => x.Value);
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

    }
}
