// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Associates an evaluated item with its originating Include's evaluation-local identity and a cached path key.
    /// </summary>
    /// <param name="item">The evaluated item represented by this record.</param>
    /// <param name="includeId">The Include table index assigned when the originating declaration was processed.</param>
    /// <param name="normalizedItemValue">
    ///  An already computed path key for <paramref name="item"/>, or <see langword="null"/> to compute it on first access.
    /// </param>
    /// <remarks>
    ///  Evaluation snapshots can share this record. Its item reference and Include identity do not change, but
    ///  the referenced item itself is not made immutable by this type. Updates must clone the item before using
    ///  <see cref="WithItem"/> so that earlier snapshots retain their item state.
    ///  <para>
    ///   Initializing the derived path cache is safe for shared records because it does not change their logical
    ///   item state. Item identity and project directory must remain unchanged while the record is shared.
    ///  </para>
    /// </remarks>
    private sealed class ItemData(I item, int includeId, NormalizedItemPath? normalizedItemValue = null)
    {
        /// <summary>
        ///  Gets the evaluated item for this state, which must be cloned before changing metadata in a later state.
        /// </summary>
        public I Item => item;

        /// <summary>
        ///  Gets the originating Include's table index, also used to restore declaration order across item types.
        /// </summary>
        public int IncludeId => includeId;

        /// <summary>
        ///  The lazily computed path key shared by all snapshots referencing this record, or <see langword="null"/> before first access.
        /// </summary>
        private NormalizedItemPath? _normalizedItemValue = normalizedItemValue;

        /// <summary>
        ///  Creates a record for a metadata-only replacement item, retaining the Include identity and path-cache state.
        /// </summary>
        /// <param name="item">The replacement item, with the same evaluated include and project directory.</param>
        /// <returns>
        ///  A new item record with the replacement item and this record's Include identity and path-cache state.
        /// </returns>
        /// <remarks>
        ///  An initialized path key is reused; replacement does not force an uninitialized key to be computed.
        /// </remarks>
        public ItemData WithItem(I item) => new(item, IncludeId, _normalizedItemValue);

        /// <summary>
        ///  Gets the lazily computed normalized item path and cached hash used by indexed Removes and literal Update batches.
        /// </summary>
        /// <remarks>
        ///  The key is computed from the item's evaluated include and project directory only when first requested.
        ///  Initialization through enumeration or a matching callback persists on the shared record without
        ///  replacing any collection entries. Metadata-only clones carry forward an already initialized key.
        /// </remarks>
        public NormalizedItemPath NormalizedItemValue
            => _normalizedItemValue ??= new NormalizedItemPath(
                FileUtilities.NormalizePathForComparisonNoThrow(Item.EvaluatedInclude, Item.ProjectDirectory));
    }
}
