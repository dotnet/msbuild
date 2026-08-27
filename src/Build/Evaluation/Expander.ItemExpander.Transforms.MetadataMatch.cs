// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

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
            private readonly struct MetadataMatch
            {
                private readonly ItemSpecModifierKind _modifierKind;

                public MetadataMatch(int index, int length, string name)
                {
                    Index = index;
                    Length = length;
                    Name = name;
                    IsDerivableItemSpecModifier = ItemSpecModifiers.TryGetDerivableModifierKind(name, out _modifierKind);
                }

                public MetadataMatch(string name)
                    : this(index: 0, name.Length + QuotedExpressionSurroundCharCount, name)
                {
                }

                /// <summary>
                ///  Gets the inner value of the match.
                /// </summary>
                internal string Name { get; }

                /// <summary>
                ///  Gets the index of the match in the original string.
                ///  If we have an exact string match, this will be 0.
                /// </summary>
                internal int Index { get; }

                /// <summary>
                ///  Gets the length of the match in the original string.
                ///  If we have an exact string match, this computed to match the original input.
                /// </summary>
                internal int Length { get; }

                /// <summary>
                ///  Gets whether the metadata name identifies a derivable item-spec modifier.
                /// </summary>
                internal bool IsDerivableItemSpecModifier { get; }

                /// <summary>
                ///  Gets the item-spec modifier kind when <see cref="IsDerivableItemSpecModifier"/> is
                ///  <see langword="true"/>.
                /// </summary>
                internal ItemSpecModifierKind ModifierKind => _modifierKind;
            }
        }
    }
}
