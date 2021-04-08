using System.Collections.Generic;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class ParameterMatchDisposition
    {
        internal string Name { get; private set; }

        internal string Value { get; private set; }

        internal ParameterNameDisposition NameDisposition { get; private set; }

        internal ParameterValueDisposition ValueDisposition { get; private set; }

        internal static IReadOnlyList<ParameterMatchDisposition> FromTemplateMatchInfo(ITemplateMatchInfo templateMatchInfo)
        {
            List<ParameterMatchDisposition> parameterMatchDispositions = new List<ParameterMatchDisposition>();

            foreach (MatchInfo matchEntry in templateMatchInfo.MatchDisposition)
            {
                if (matchEntry.Location != MatchLocation.OtherParameter)
                {
                    continue;
                }

                // the name is either an exact match or it's invalid
                ParameterNameDisposition nameDisposition = matchEntry.Kind == MatchKind.InvalidParameterName ? ParameterNameDisposition.Invalid : ParameterNameDisposition.Exact;

                ParameterValueDisposition valueDisposition;

                switch (matchEntry.Kind)
                {
                    case MatchKind.Exact:
                    case MatchKind.SingleStartsWith:    // value resolved to a single choice value
                        valueDisposition = ParameterValueDisposition.Valid;
                        break;
                    case MatchKind.AmbiguousParameterValue:
                        valueDisposition = ParameterValueDisposition.Ambiguous;
                        break;
                    case MatchKind.InvalidParameterValue:
                        valueDisposition = ParameterValueDisposition.Mismatch;
                        break;
                    case MatchKind.InvalidParameterName:
                        valueDisposition = ParameterValueDisposition.None;
                        break;
                    // Default can (theoretically) catch these values, but they should ever be encountered here:
                    //case MatchKind.Mismatch:        // this should only occur for template language
                    //case MatchKind.Unspecified:     // only occurs when a processing error occurs.
                    //case MatchKind.Partial:         // only used for matching the name fields.
                    default:
                        valueDisposition = ParameterValueDisposition.None;
                        break;
                }

                ParameterMatchDisposition matchDisposition = new ParameterMatchDisposition()
                {
                    Name = matchEntry.InputParameterName,
                    Value = matchEntry.ParameterValue,
                    NameDisposition = nameDisposition,
                    ValueDisposition = valueDisposition
                };

                parameterMatchDispositions.Add(matchDisposition);
            }

            return parameterMatchDispositions;
        }
    }
}
