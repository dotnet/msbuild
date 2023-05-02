// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.Commands;

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

        internal static IEnumerable<string> GetInvalidParameterNames(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition
                .OfType<ParameterMatchInfo>()
                .Where(match => match.Kind == MatchKind.InvalidName)
                .Select(match => match.Name);
        }

        internal static bool HasMismatchOnConstraints(this ITemplateMatchInfo templateMatchInfo)
        {
            var constraintsMatches = templateMatchInfo.MatchDisposition.Where(mi => mi.Name.StartsWith(MatchInfo.BuiltIn.Constraint));
            var otherMatches = templateMatchInfo.MatchDisposition.Where(mi => !mi.Name.StartsWith(MatchInfo.BuiltIn.Constraint));

            if (!constraintsMatches.Any())
            {
                return false;
            }
            if (otherMatches.Any(mi => mi.Kind == MatchKind.Mismatch || mi.Kind == MatchKind.InvalidName || mi.Kind == MatchKind.InvalidValue))
            {
                //there are other mismatches than on constraints
                return false;
            }
            return constraintsMatches.Any(mi => mi.Kind == MatchKind.Mismatch);
        }

        internal static bool HasMismatchOnListFilters(this ITemplateMatchInfo templateMatchInfo)
        {
            IEnumerable<string> supportedFilters = BaseListCommand.SupportedFilters.OfType<TemplateFilterOptionDefinition>().Select(f => f.MatchInfoName);

            var filterMatches = templateMatchInfo.MatchDisposition.Where(mi => supportedFilters.Any(f => f == mi.Name));
            var otherMatches = templateMatchInfo.MatchDisposition.Where(mi => !supportedFilters.Any(f => f == mi.Name));

            if (!filterMatches.Any())
            {
                return false;
            }
            if (otherMatches.Any(mi => mi.Kind == MatchKind.Mismatch || mi.Kind == MatchKind.InvalidName || mi.Kind == MatchKind.InvalidValue))
            {
                //there are other mismatches than on constraints
                return false;
            }
            return filterMatches.Any(mi => mi.Kind == MatchKind.Mismatch);
        }

    }
}
