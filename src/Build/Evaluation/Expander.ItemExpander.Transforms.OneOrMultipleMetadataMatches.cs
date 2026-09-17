// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    private static partial class ItemExpander
    {
        private static partial class Transforms
        {
            /// <summary>
            /// A discriminated union between one exact, one partial, or multiple matches.
            /// </summary>
            private readonly struct OneOrMultipleMetadataMatches
            {
                public static OneOrMultipleMetadataMatches None => default;

                public MetadataMatch Single { get; }
                public List<MetadataMatch>? Multiple { get; }
                public MetadataMatchType Type { get; }

                public OneOrMultipleMetadataMatches()
                {
                    Type = MetadataMatchType.None;
                }

                public OneOrMultipleMetadataMatches(string name)
                {
                    Type = MetadataMatchType.ExactSingle;
                    Single = new MetadataMatch(name);
                }

                public OneOrMultipleMetadataMatches(string quotedExpressionFunction, int matchIndex, int matchLength, string name)
                {
                    // We know we have a full string match when our extracted name is the same length as the input
                    // string minus the surrounding characters.
                    Type = quotedExpressionFunction.Length == name.Length + QuotedExpressionSurroundCharCount
                        ? MetadataMatchType.ExactSingle
                        : MetadataMatchType.InexactSingle;

                    Single = new MetadataMatch(matchIndex, matchLength, name);
                }

                public OneOrMultipleMetadataMatches(List<MetadataMatch> allMatches)
                {
                    Type = MetadataMatchType.Multiple;
                    Multiple = allMatches;
                }
            }
        }
    }
}
