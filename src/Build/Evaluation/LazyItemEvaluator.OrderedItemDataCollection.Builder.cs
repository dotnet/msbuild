// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed partial class OrderedItemDataCollection
    {
        /// <summary>
        /// A mutable and enumerable version of <see cref="OrderedItemDataCollection"/>.
        /// </summary>
        internal sealed class Builder : IEnumerable<ItemData>
        {
            /// <summary>
            /// The list of items in the collection. Defines the enumeration order.
            /// </summary>
            private ImmutableList<ItemData>.Builder _listBuilder;

            /// <summary>
            ///  The normalized item paths present in the collection.
            /// </summary>
            private HashSet<string> _normalizedItemPaths;

            internal Builder(ImmutableList<ItemData>.Builder listBuilder)
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
            public HashSet<string> NormalizedItemPaths
            {
                get
                {
                    if (_normalizedItemPaths is null)
                    {
                        _normalizedItemPaths = [with(StringComparer.OrdinalIgnoreCase)];
                        for (int i = 0; i < _listBuilder.Count; i++)
                        {
                            ItemData itemData = _listBuilder[i];
                            _normalizedItemPaths.Add(itemData.NormalizedItemValue);

                            // Persist the lazily computed normalized value for later removal scans.
                            _listBuilder[i] = itemData;
                        }
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
            public void RemoveAll(ISet<string> itemPathsToRemove)
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
        }
    }
}
