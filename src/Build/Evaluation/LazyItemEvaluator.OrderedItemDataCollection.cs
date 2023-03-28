// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        /// <summary>
        /// A collection of ItemData that maintains insertion order and internally optimizes some access patterns, e.g. bulk removal
        /// based on normalized item values.
        /// </summary>
        internal sealed class OrderedItemDataCollection
        {
            #region Inner types

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
                /// A dictionary of items keyed by their normalized value.
                /// </summary>
                private Dictionary<string, ItemDataCollectionValue<I>> _dictionaryBuilder;

                internal Builder(ImmutableList<ItemData>.Builder listBuilder)
                {
                    _listBuilder = listBuilder;
                }

                #region IEnumerable implementation

                private ImmutableList<ItemData>.Enumerator GetEnumerator() => _listBuilder.GetEnumerator();
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
                        // Update the dictionary if it exists.
                        if (_dictionaryBuilder is not null)
                        {
                            ItemData oldItemData = _listBuilder[index];
                            string oldNormalizedValue = oldItemData.NormalizedItemValue;
                            string newNormalizedValue = value.NormalizedItemValue;
                            if (!string.Equals(oldNormalizedValue, newNormalizedValue, StringComparison.OrdinalIgnoreCase))
                            {
                                // Normalized values are different - delete from the old entry and add to the new entry.
                                ItemDataCollectionValue<I> oldDictionaryEntry = _dictionaryBuilder[oldNormalizedValue];
                                oldDictionaryEntry.Delete(oldItemData.Item);
                                if (oldDictionaryEntry.IsEmpty)
                                {
                                    _dictionaryBuilder.Remove(oldNormalizedValue);
                                }
                                else
                                {
                                    _dictionaryBuilder[oldNormalizedValue] = oldDictionaryEntry;
                                }

                                ItemDataCollectionValue<I> newDictionaryEntry = _dictionaryBuilder[newNormalizedValue];
                                newDictionaryEntry.Add(value.Item);
                                _dictionaryBuilder[newNormalizedValue] = newDictionaryEntry;
                            }
                            else
                            {
                                // Normalized values are the same - replace the item in the entry.
                                ItemDataCollectionValue<I> dictionaryEntry = _dictionaryBuilder[newNormalizedValue];
                                dictionaryEntry.Replace(oldItemData.Item, value.Item);
                                _dictionaryBuilder[newNormalizedValue] = dictionaryEntry;
                            }
                        }
                        _listBuilder[index] = value;
                    }
                }

                /// <summary>
                /// Gets or creates a dictionary keyed by normalized values.
                /// </summary>
                public Dictionary<string, ItemDataCollectionValue<I>> Dictionary
                {
                    get
                    {
                        if (_dictionaryBuilder == null)
                        {
                            _dictionaryBuilder = new Dictionary<string, ItemDataCollectionValue<I>>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < _listBuilder.Count; i++)
                            {
                                ItemData itemData = _listBuilder[i];
                                AddToDictionary(ref itemData);
                                _listBuilder[i] = itemData;
                            }
                        }
                        return _dictionaryBuilder;
                    }
                }

                public void Add(ItemData data)
                {
                    if (_dictionaryBuilder is not null)
                    {
                        AddToDictionary(ref data);
                    }
                    _listBuilder.Add(data);
                }

                public void Clear()
                {
                    _listBuilder.Clear();
                    _dictionaryBuilder?.Clear();
                }

                /// <summary>
                /// Removes all items passed in a collection.
                /// </summary>
                public void RemoveAll(ICollection<I> itemsToRemove)
                {
                    _listBuilder.RemoveAll(item => itemsToRemove.Contains(item.Item));
                    // This is a rare operation, don't bother updating the dictionary for now. It will be recreated as needed.
                    _dictionaryBuilder = null;
                }

                /// <summary>
                /// Removes all items whose normalized path is passed in a collection.
                /// </summary>
                public void RemoveAll(ICollection<string> itemPathsToRemove)
                {
                    var dictionary = Dictionary;
                    HashSet<I> itemsToRemove = null;
                    foreach (string itemValue in itemPathsToRemove)
                    {
                        if (dictionary.TryGetValue(itemValue, out var multiItem))
                        {
                            foreach (I item in multiItem)
                            {
                                itemsToRemove ??= new HashSet<I>();
                                itemsToRemove.Add(item);
                            }
                            _dictionaryBuilder.Remove(itemValue);
                        }
                    }

                    if (itemsToRemove is not null)
                    {
                        _listBuilder.RemoveAll(item => itemsToRemove.Contains(item.Item));
                    }
                }

                /// <summary>
                /// Creates an immutable view of this collection.
                /// </summary>
                public OrderedItemDataCollection ToImmutable()
                {
                    return new OrderedItemDataCollection(_listBuilder.ToImmutable());
                }

                private void AddToDictionary(ref ItemData itemData)
                {
                    string key = itemData.NormalizedItemValue;

                    if (!_dictionaryBuilder.TryGetValue(key, out var dictionaryValue))
                    {
                        dictionaryValue = new ItemDataCollectionValue<I>(itemData.Item);
                    }
                    else
                    {
                        dictionaryValue.Add(itemData.Item);
                    }
                    _dictionaryBuilder[key] = dictionaryValue;
                }
            }

            #endregion

            /// <summary>
            /// The list of items in the collection. Defines the enumeration order.
            /// </summary>
            private ImmutableList<ItemData> _list;

            private OrderedItemDataCollection(ImmutableList<ItemData> list)
            {
                _list = list;
            }

            /// <summary>
            /// Creates a new mutable collection.
            /// </summary>
            public static Builder CreateBuilder()
            {
                return new Builder(ImmutableList.CreateBuilder<ItemData>());
            }

            /// <summary>
            /// Creates a mutable view of this collection. Changes made to the returned builder are not reflected in this collection.
            /// </summary>
            public Builder ToBuilder()
            {
                return new Builder(_list.ToBuilder());
            }
        }
    }
}
