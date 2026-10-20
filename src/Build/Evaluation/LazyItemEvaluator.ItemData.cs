// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Associates an evaluated item with its originating Include, declaration order, condition result, and cached path key.
    /// </summary>
    /// <param name="item">The evaluated item represented by this record.</param>
    /// <param name="originatingItemElement">The Include element that originally produced the item, retained across Updates.</param>
    /// <param name="elementOrder">The originating Include's order in item declaration processing, shared by its produced items.</param>
    /// <param name="conditionResult">Whether both the originating item-group and item conditions evaluated to <see langword="true"/>.</param>
    /// <param name="normalizedItemValue">
    ///  An already computed path key for <paramref name="item"/>, or <see langword="null"/> to compute it on first access.
    /// </param>
    /// <remarks>
    ///  Evaluation snapshots can share this record. Its item reference and provenance do not change, but the
    ///  referenced item itself is not made immutable by this type. Updates must use <see cref="Clone"/> before
    ///  changing metadata so that earlier snapshots retain their item state.
    ///  <para>
    ///   Initializing the derived path cache is safe for shared records because it does not change their logical
    ///   item state. Item identity and project directory must remain unchanged while the record is shared.
    ///  </para>
    /// </remarks>
    private sealed class ItemData(
        I item,
        ProjectItemElement originatingItemElement,
        int elementOrder,
        bool conditionResult,
        NormalizedItemPath? normalizedItemValue = null)
    {
        /// <summary>
        ///  Gets the evaluated item for this state, which must be cloned before changing metadata in a later state.
        /// </summary>
        public I Item => item;

        /// <summary>
        ///  Gets the Include element that originally produced the item, rather than a subsequent Update element.
        /// </summary>
        public ProjectItemElement OriginatingItemElement => originatingItemElement;

        /// <summary>
        ///  Gets the originating Include's order, used to merge different item types back into declaration order.
        /// </summary>
        public int ElementOrder => elementOrder;

        /// <summary>
        ///  Gets whether the originating item-group and item conditions were both <see langword="true"/>.
        /// </summary>
        /// <remarks>
        ///  Records whose conditions were <see langword="false"/> may be retained for design-time evaluation, but are excluded
        ///  from matched-item projections.
        /// </remarks>
        public bool ConditionResult => conditionResult;

        /// <summary>
        ///  The lazily computed path key shared by all snapshots referencing this record, or <see langword="null"/> before first access.
        /// </summary>
        private NormalizedItemPath? _normalizedItemValue = normalizedItemValue;

        /// <summary>
        ///  Creates a new record and cloned item for metadata mutation without altering an earlier snapshot.
        /// </summary>
        /// <param name="itemFactory">The factory used to clone the evaluated item and its metadata.</param>
        /// <param name="initialItemElementForFactory">The element to assign back to the factory after creating the clone.</param>
        /// <returns>
        ///  A new item record retaining this record's provenance, declaration order, condition result, and path-cache state.
        /// </returns>
        /// <remarks>
        ///  The factory temporarily uses <see cref="OriginatingItemElement"/> so the clone's defining-project
        ///  metadata continues to describe the original Include rather than the Update that requested the clone.
        ///  An initialized path key is reused; cloning does not force an uninitialized key to be computed.
        /// </remarks>
        public ItemData Clone(IItemFactory<I, I> itemFactory, ProjectItemElement initialItemElementForFactory)
        {
            itemFactory.ItemElement = OriginatingItemElement;
            var clonedItem = itemFactory.CreateItem(Item, OriginatingItemElement.ContainingProject.FullPath);
            itemFactory.ItemElement = initialItemElementForFactory;

            return new ItemData(clonedItem, OriginatingItemElement, ElementOrder, ConditionResult, _normalizedItemValue);
        }

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
