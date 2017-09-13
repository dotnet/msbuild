using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public static class TemplateMatchInfoExtensions
    {
        public static bool HasParameterMismatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter
                                       && (x.Kind != MatchKind.Exact && x.Kind != MatchKind.AmbiguousParameterValue));
        }

        public static bool HasAmbiguousParameterValueMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.AmbiguousParameterValue);
        }

        public static bool IsInvokableMatch(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Count > 0
                            && templateMatchInfo.MatchDisposition.All(x =>
                                x.Kind == MatchKind.Exact
                                ||
                                (   // these locations can have partial or exact matches.
                                    x.Kind == MatchKind.Partial
                                    && (x.Location == MatchLocation.Name || x.Location == MatchLocation.ShortName || x.Location == MatchLocation.Classification)
                                )
                            );
        }

        public static IReadOnlyList<string> GetInvalidParameterNames(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Where(x => x.Kind == MatchKind.InvalidParameterName)
                                                   .Select(x => x.ChoiceIfLocationIsOtherChoice).ToList();
        }

        public static bool HasParseError(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Any(x => x.Kind == MatchKind.Unspecified);
        }

        public static string GetParseError(this ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition.Where(x => x.Kind == MatchKind.Unspecified).Select(x => x.AdditionalInformation).FirstOrDefault();
        }

        // This is analogous to INewCommandInput.InputTemplateParams
        public static IReadOnlyDictionary<string, string> GetValidTemplateParameters(this ITemplateMatchInfo templateMatchInfo)
        { 
            return templateMatchInfo.MatchDisposition.Where(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.Exact)
                                    .ToDictionary(x => x.ChoiceIfLocationIsOtherChoice, x => x.ParameterValue);
        }
    }
}
