// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal static class TemplateMatchInfoExtensions
    {
        // True if name is explicitly mismatched.
        // Partial matches are ok. No disposition on name is also ok.
        internal static bool HasNameMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.Name && x.Kind == MatchKind.Mismatch);
        }

        internal static bool HasParameterMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter
                                       && x.Kind != MatchKind.Exact && x.Kind != MatchKind.AmbiguousParameterValue && x.Kind != MatchKind.SingleStartsWith);
        }

        internal static bool HasContextMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.Context && x.Kind == MatchKind.Mismatch);
        }

        internal static bool HasLanguageMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.Language && x.Kind == MatchKind.Mismatch);
        }

        internal static bool HasDefaultLanguageMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.DispositionOfDefaults.Any(x => x.Location == MatchLocation.DefaultLanguage && x.Kind == MatchKind.Exact);
        }

        internal static bool HasInvalidParameterName(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.InvalidParameterName);
        }

        internal static bool HasBaselineMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.Baseline && x.Kind == MatchKind.Mismatch);
        }

        internal static bool HasAuthorMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.Author && x.Kind == MatchKind.Mismatch);
        }

        internal static bool HasTagsMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.Classification && x.Kind == MatchKind.Mismatch);
        }

        internal static bool HasAmbiguousParameterValueMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.AmbiguousParameterValue);
        }

        internal static bool IsInvokableMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Count > 0
                            && templateMatchInfo.MatchDisposition.All(x =>
                                x.Kind == MatchKind.Exact
                                ||
                                    // these locations can have partial or exact matches.
                                    x.Kind == MatchKind.Partial
                                    && (x.Location == MatchLocation.Name || x.Location == MatchLocation.ShortName || x.Location == MatchLocation.Classification || x.Location == MatchLocation.Author)

                                ||

                                    x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.SingleStartsWith

                            );
        }

        internal static IReadOnlyList<string> GetInvalidParameterNames(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Where(x => x.Kind == MatchKind.InvalidParameterName)
                                                   .Select(x => x.InputParameterName).ToList();
        }

        internal static bool HasParseError(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Kind == MatchKind.Unspecified);
        }

        internal static string GetParseError(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Where(x => x.Kind == MatchKind.Unspecified).Select(x => x.AdditionalInformation).FirstOrDefault();
        }

        // This is analogous to INewCommandInput.InputTemplateParams
        internal static IReadOnlyDictionary<string, string> GetValidTemplateParameters(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Where(x => x.Location == MatchLocation.OtherParameter && (x.Kind == MatchKind.Exact || x.Kind == MatchKind.SingleStartsWith))
                                    .ToDictionary(x => x.InputParameterName, x => x.ParameterValue);
        }

        internal static bool IsContextOnlyMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.All(x => x.Location == MatchLocation.Context && x.Kind == MatchKind.Exact
                                                                || x.Kind == MatchKind.Mismatch);
        }

        internal static bool IsNameOnlyMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.All(x => x.Location == MatchLocation.Name && x.Kind == MatchKind.Exact
                                                                || x.Kind == MatchKind.Mismatch);
        }

        internal static bool IsNameOrContextMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.Name && x.Kind == MatchKind.Exact
                                                                || x.Location == MatchLocation.Context && x.Kind == MatchKind.Exact);
        }

        internal static bool IsMatchExceptContext(this ITemplateMatchInfo templateMatchInfo)
        {
            if (templateMatchInfo.MatchDisposition.Count == 0)
            {
                return false;
            }

            bool hasContextMismatch = false;

            foreach (MatchInfo disposition in templateMatchInfo.MatchDisposition)
            {
                if (disposition.Location == MatchLocation.Context)
                {
                    if (disposition.Kind == MatchKind.Exact)
                    {
                        return false;
                    }
                    else
                    {
                        hasContextMismatch = true;
                    }
                }
                else if (disposition.Kind == MatchKind.Mismatch)
                {
                    return false;
                }
            }

            return hasContextMismatch;
        }

        internal static bool IsMatchExceptLanguage(this ITemplateMatchInfo templateMatchInfo)
        {
            if (templateMatchInfo.MatchDisposition.Count == 0)
            {
                return false;
            }

            bool hasLanguageMismatch = false;

            foreach (MatchInfo disposition in templateMatchInfo.MatchDisposition)
            {
                if (disposition.Location == MatchLocation.Language)
                {
                    if (disposition.Kind == MatchKind.Exact)
                    {
                        return false;
                    }
                    else
                    {
                        hasLanguageMismatch = true;
                    }
                }
                else if (disposition.Kind == MatchKind.Mismatch)
                {
                    return false;
                }
            }

            return hasLanguageMismatch;
        }

        // Returns true if there is a context mismatch and no other mismatches, false otherwise.
        // Note: there must be at least one disposition that is not mismatch, in addition to the context mismatch.
        internal static bool IsPartialMatchExceptContext(this ITemplateMatchInfo templateMatchInfo)
        {
            if (templateMatchInfo.MatchDisposition.Count == 0)
            {
                return false;
            }

            bool hasContextMismatch = false;
            bool hasOtherThanMismatch = false;

            foreach (MatchInfo disposition in templateMatchInfo.MatchDisposition)
            {
                if (disposition.Location == MatchLocation.Context)
                {
                    if (disposition.Kind == MatchKind.Exact)
                    {
                        return false;
                    }
                    else
                    {
                        hasContextMismatch = true;
                    }
                }
                else if (disposition.Kind != MatchKind.Mismatch)
                {
                    hasOtherThanMismatch = true;
                }
            }

            return hasOtherThanMismatch && hasContextMismatch;
        }

        internal static bool HasNameOrClassificationMatchOrPartialMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any((x => (x.Location == MatchLocation.Name || x.Location == MatchLocation.ShortName || x.Location == MatchLocation.Classification) && (x.Kind == MatchKind.Exact || x.Kind == MatchKind.Partial)));
        }

        internal static bool HasAnyMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(m => m.Kind == MatchKind.Mismatch);
        }
    }
}
