// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed partial class OrderedItemDataCollection
    {
        /// <summary>
        ///  Mutable replay state over a piece-list editor, with a lazy path index for Remove operations.
        /// </summary>
        internal sealed class Builder : IEnumerable<ItemData>
        {
            /// <summary>
            ///  The current item sequence; publication preserves its state for later historical reads.
            /// </summary>
            private readonly ImmutablePieceList<ItemData>.Editor _listBuilder;

            /// <summary>
            ///  The normalized item paths present in the collection.
            /// </summary>
            private HashSet<NormalizedItemPath> _normalizedItemPaths;

            internal Builder(ImmutablePieceList<ItemData>.Editor listBuilder)
            {
                _listBuilder = listBuilder;
            }

            #region IEnumerable implementation

            IEnumerator<ItemData> IEnumerable<ItemData>.GetEnumerator() => _listBuilder.GetEnumerator();

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _listBuilder.GetEnumerator();

            #endregion

            public int Count => _listBuilder.Count;

            public ItemData this[int index]
            {
                get
                {
                    return _listBuilder[index];
                }

                set
                {
                    if (_normalizedItemPaths is not null
                        && !_normalizedItemPaths.Comparer.Equals(_listBuilder[index].NormalizedItemValue, value.NormalizedItemValue))
                    {
                        // Another item may still have the old path, so rebuild rather than removing that path.
                        _normalizedItemPaths = null;
                    }

                    _listBuilder[index] = value;
                }
            }

            /// <summary>
            ///  Gets or creates an index of the normalized item paths.
            /// </summary>
            public HashSet<NormalizedItemPath> NormalizedItemPaths
            {
                get
                {
                    if (_normalizedItemPaths is null)
                    {
                        HashSet<NormalizedItemPath> paths = [];
                        foreach (ItemData itemData in _listBuilder)
                        {
                            paths.Add(itemData.NormalizedItemValue);
                        }

                        _normalizedItemPaths = paths;
                    }
                    return _normalizedItemPaths;
                }
            }

            public void Add(ItemData data)
            {
                if (_normalizedItemPaths is not null)
                {
                    _normalizedItemPaths.Add(data.NormalizedItemValue);
                }

                _listBuilder.Add(data);
            }

            public void Clear()
            {
                _listBuilder.Clear();
                _normalizedItemPaths?.Clear();
            }

            /// <summary>
            ///  Replaces matching items sequentially, preserving a materialized path index when paths remain unchanged.
            /// </summary>
            /// <param name="match">The predicate selecting items to replace.</param>
            /// <param name="update">The replacement operation for each matching item.</param>
            /// <returns>
            ///  The number of matched items.
            /// </returns>
            /// <exception cref="ArgumentNullException">Either callback is <see langword="null"/>.</exception>
            public int UpdateAll(Predicate<ItemData> match, Func<ItemData, ItemData> update)
            {
                ArgumentNullException.ThrowIfNull(match);
                ArgumentNullException.ThrowIfNull(update);

                return _normalizedItemPaths is null
                    ? _listBuilder.UpdateAll(match, update)
                    : UpdateAllWithPathIndex(match, update);
            }

            /// <summary>
            /// Removes all items passed in a collection.
            /// </summary>
            public void RemoveAll(ICollection<I> itemsToRemove)
            {
                _listBuilder.RemoveAll(item => itemsToRemove.Contains(item.Item));
                _normalizedItemPaths = null;
            }

            /// <summary>
            ///  Removes all items whose normalized path is in <paramref name="itemPathsToRemove"/>.
            /// </summary>
            /// <param name="itemPathsToRemove">The matched paths, using the normalized-path index's comparison rules.</param>
            public void RemoveAll(ISet<NormalizedItemPath> itemPathsToRemove)
            {
                if (itemPathsToRemove.Count == 0)
                {
                    return;
                }

                _listBuilder.RemoveAll(item => itemPathsToRemove.Contains(item.NormalizedItemValue));
                _normalizedItemPaths?.ExceptWith(itemPathsToRemove);
            }

            /// <summary>
            /// Creates an immutable view of this collection.
            /// </summary>
            public OrderedItemDataCollection ToImmutable()
            {
                return new OrderedItemDataCollection(_listBuilder.ToImmutable());
            }

            /// <summary>
            ///  Updates items while checking whether an existing path index needs to be rebuilt after commit.
            /// </summary>
            /// <param name="match">The validated predicate selecting items.</param>
            /// <param name="update">The validated replacement operation.</param>
            /// <returns>
            ///  The number of matched items.
            /// </returns>
            /// <remarks>
            ///  The index is invalidated only after successful replacement. Metadata-only updates retain it,
            ///  and builders without an index avoid this additional callback and normalization work.
            /// </remarks>
            private int UpdateAllWithPathIndex(Predicate<ItemData> match, Func<ItemData, ItemData> update)
            {
                HashSet<NormalizedItemPath> paths = _normalizedItemPaths;
                bool pathsChanged = false;
                int matched = _listBuilder.UpdateAll(match, itemData =>
                {
                    ItemData replacement = update(itemData);
                    pathsChanged |= !paths.Comparer.Equals(itemData.NormalizedItemValue, replacement.NormalizedItemValue);
                    return replacement;
                });

                if (pathsChanged)
                {
                    _normalizedItemPaths = null;
                }

                return matched;
            }
        }
    }
}
