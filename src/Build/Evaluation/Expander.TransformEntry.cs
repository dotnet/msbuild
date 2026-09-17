// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  An entry in the transform pipeline, carrying a value and optionally the item
    ///  it was derived from.
    /// </summary>
    /// <remarks>
    /// <para>
    ///  <see cref="Value"/> may be null when the entry represents a filtered-out item
    ///  that is retained for null-entry inclusion.
    /// </para>
    /// <para>
    ///  <see cref="Item"/> is null when the entry was synthesized by a transform
    ///  (e.g. Combine, GetPathsOfAllDirectoriesAbove) and there is no base item
    ///  to carry metadata from.
    /// </para>
    /// </remarks>
    internal readonly struct TransformEntry(string? value, I? item)
    {
        /// <summary>
        ///  Gets the current string value (escaped) at this point in the pipeline.
        /// </summary>
        public string? Value { get; } = value;

        /// <summary>
        ///  Gets the item this entry was derived from, used to carry metadata forward
        ///  through the pipeline.
        /// </summary>
        public I? Item { get; } = item;

        public void Deconstruct(out string? value, out I? item)
        {
            value = Value;
            item = Item;
        }
    }
}
