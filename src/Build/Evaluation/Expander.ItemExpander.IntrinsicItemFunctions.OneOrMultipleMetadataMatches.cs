// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.RegularExpressions;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private static partial class ItemExpander
    {
        internal static partial class IntrinsicItemFunctions
        {
            /// <summary>
            /// A discriminated union between one exact, one partial, or multiple matches.
            /// </summary>
            private readonly struct OneOrMultipleMetadataMatches
            {
                public OneOrMultipleMetadataMatches()
                {
                    Type = MetadataMatchType.None;
                }

                public OneOrMultipleMetadataMatches(string name)
                {
                    Type = MetadataMatchType.ExactSingle;
                    Single = new MetadataMatch(name);
                }

                public OneOrMultipleMetadataMatches(string quotedExpressionFunction, Match match, string name)
                {
                    // We know we have a full string match when our extracted name is the same length as the input
                    // string minus the surrounding characters.
                    Type = quotedExpressionFunction.Length == name.Length + QuotedExpressionSurroundCharCount
                        ? MetadataMatchType.ExactSingle
                        : MetadataMatchType.InexactSingle;
                    Single = new MetadataMatch(match, name);
                }

                public OneOrMultipleMetadataMatches(List<MetadataMatch> allMatches)
                {
                    Type = MetadataMatchType.Multiple;
                    Multiple = allMatches;
                }

                internal MetadataMatch Single { get; }

                internal List<MetadataMatch> Multiple { get; }

                internal MetadataMatchType Type { get; }
            }
        }
    }
}
