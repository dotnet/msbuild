// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    private static partial class ItemExpander
    {
        private static partial class Transforms
        {
            /// <summary>
            ///  Represents a single match. Whether it was cached or from a manual scan should be transparent
            ///  since we simulate the length calculation.
            /// </summary>
            private readonly struct MetadataMatch(int index, int length, string name)
            {
                public MetadataMatch(string name)
                    : this(index: 0, name.Length + QuotedExpressionSurroundCharCount, name)
                {
                }

                /// <summary>
                ///  Gets the inner value of the match.
                /// </summary>
                internal string Name => name;

                /// <summary>
                ///  Gets the index of the match in the original string.
                ///  If we have an exact string match, this will be 0.
                /// </summary>
                internal int Index => index;

                /// <summary>
                ///  Gets the length of the match in the original string.
                ///  If we have an exact string match, this computed to match the original input.
                /// </summary>
                internal int Length => length;
            }
        }
    }
}
