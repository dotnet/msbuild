// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private static partial class ItemExpander
    {
        private static partial class Transforms
        {
            /// <summary>
            /// Represents a single match. Whether it was cached or from a Regex should be transparent
            /// since we simulate the length calculation.
            /// </summary>
            private readonly struct MetadataMatch
            {
                public MetadataMatch(string name)
                {
                    Name = name;
                    Index = 0;
                    Length = name.Length + QuotedExpressionSurroundCharCount;
                }

                public MetadataMatch(Match match, string name)
                {
                    Name = name;
                    Index = match.Index;
                    Length = match.Length;
                }

                /// <summary>
                /// The inner value of the match.
                /// </summary>
                internal string Name { get; }

                /// <summary>
                /// The index of the match in the original string.
                /// If we have an exact string match, this will be 0.
                /// </summary>
                internal int Index { get; }

                /// <summary>
                /// The length of the match in the original string.
                /// If we have an exact string match, this computed to match the original input.
                /// </summary>
                internal int Length { get; }
            }
        }
    }
}
