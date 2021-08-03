// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

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
            /// An efficient multi-value wrapper holding one or more items.
            /// </summary>
            internal struct DictionaryValue
            {
                /// <summary>
                /// A non-allocating enumerator for the multi-value.
                /// </summary>
                public struct Enumerator : IEnumerator<I>
                {
                    private object _value;
                    private int _index;

                    public Enumerator(object value)
                    {
                        _value = value;
                        _index = -1;
                    }

                    public I Current => (_value is IList<I> list) ? list[_index] : (I)_value;
                    object System.Collections.IEnumerator.Current => Current;

                    public void Dispose()
                    { }

                    public bool MoveNext()
                    {
                        int count = (_value is IList<I> list) ? list.Count : 1;
                        if (_index + 1 < count)
                        {
                            _index++;
                            return true;
                        }
                        return false;
                    }

                    public void Reset()
                    {
                        _index = -1;
                    }
                }

                /// <summary>
                /// Holds one value or a list of values.
                /// </summary>
                private object _value;

                public DictionaryValue(I item)
                {
                    _value = item;
                }

                public void Add(I item)
                {
                    if (_value is not ImmutableList<I> list)
                    {
                        list = ImmutableList<I>.Empty;
                        list = list.Add((I)_value);
                    }
                    _value = list.Add(item);
                }

                public Enumerator GetEnumerator()
                {
                    return new Enumerator(_value);
                }
            }

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
                private ImmutableDictionary<string, DictionaryValue>.Builder _dictionaryBuilder;

                internal Builder(ImmutableList<ItemData>.Builder listBuilder, ImmutableDictionary<string, DictionaryValue>.Builder dictionaryBuilder)
                {
                    _listBuilder = listBuilder;
                    _dictionaryBuilder = dictionaryBuilder;
                }

                #region IEnumerable implementation

                ImmutableList<ItemData>.Enumerator GetEnumerator() => _listBuilder.GetEnumerator();
                IEnumerator<ItemData> IEnumerable<ItemData>.GetEnumerator() => _listBuilder.GetEnumerator();

                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _listBuilder.GetEnumerator();

                #endregion

                public int Count => _listBuilder.Count;

                public ItemData this[int index]
                {
                    get { return _listBuilder[index]; }
                    set
                    {
                        _listBuilder[index] = value;
                        // This is a rare operation, don't bother updating the dictionary for now. It will be recreated as needed.
                        _dictionaryBuilder = null;
                    }
                }

                public void Add(ItemData data)
                {
                    _listBuilder.Add(data);
                    if (_dictionaryBuilder != null)
                    {
                        AddToDictionary(data.Item);
                    }
                }

                public void Clear()
                {
                    _listBuilder.Clear();
                    _dictionaryBuilder?.Clear();
                }

                public void RemoveAll(ICollection<I> itemsToRemove)
                {
                    _listBuilder.RemoveAll(item => itemsToRemove.Contains(item.Item));
                    // This is a rare operation, don't bother updating the dictionary for now. It will be recreated as needed.
                    _dictionaryBuilder = null;
                }

                /// <summary>
                /// Removes items from the collection that match the given ItemSpec.
                /// </summary>
                /// <remarks>
                /// If <see cref="_dictionaryBuilder"/> does not exist yet, it is created in this method to avoid the cost of comparing each item
                /// being removed with each item already in the collection. The dictionary is kept in sync with the <see cref="_listBuilder"/>
                /// as long as practical. If an operation would result in too much of such work, the dictionary is simply dropped and recreated
                /// later if/when needed.
                /// </remarks>
                public void RemoveMatchingItems(ItemSpec<P, I> itemSpec)
                {
                    HashSet<I> items = null;
                    List<string> keysToRemove = null;
                    var dictionaryBuilder = GetOrCreateDictionaryBuilder();

                    foreach (var fragment in itemSpec.Fragments)
                    {
                        IEnumerable<string> referencedItems = fragment.GetReferencedItems();
                        if (referencedItems != null)
                        {
                            // The fragment can enumerate its referenced items, we can do dictionary lookups.
                            foreach (var spec in referencedItems)
                            {
                                string key = FileUtilities.NormalizePathForComparisonNoThrow(spec, fragment.ProjectDirectory);
                                if (dictionaryBuilder.TryGetValue(key, out var multiValue))
                                {
                                    items ??= new HashSet<I>();
                                    foreach (I item in multiValue)
                                    {
                                        items.Add(item);
                                    }
                                    keysToRemove ??= new List<string>();
                                    keysToRemove.Add(key);
                                }
                            }
                        }
                        else
                        {
                            // The fragment cannot enumerate its referenced items. Iterate over the dictionary and test each item.
                            foreach (var kvp in dictionaryBuilder)
                            {
                                if (fragment.IsMatchNormalized(kvp.Key))
                                {
                                    items ??= new HashSet<I>();
                                    foreach (I item in kvp.Value)
                                    {
                                        items.Add(item);
                                    }
                                    keysToRemove ??= new List<string>();
                                    keysToRemove.Add(kvp.Key);
                                }
                            }
                        }
                    }

                    // Finish by removing items from the list.
                    if (keysToRemove != null)
                    {
                        dictionaryBuilder.RemoveRange(keysToRemove);
                    }
                    if (items != null)
                    {
                        _listBuilder.RemoveAll(item => items.Contains(item.Item));
                    }
                }

                /// <summary>
                /// Creates an immutable view of this collection.
                /// </summary>
                public OrderedItemDataCollection ToImmutable()
                {
                    return new OrderedItemDataCollection(_listBuilder.ToImmutable(), _dictionaryBuilder?.ToImmutable());
                }

                private ImmutableDictionary<string, DictionaryValue>.Builder GetOrCreateDictionaryBuilder()
                {
                    if (_dictionaryBuilder == null)
                    {
                        _dictionaryBuilder = ImmutableDictionary.CreateBuilder<string, DictionaryValue>(StringComparer.OrdinalIgnoreCase);
                        foreach (ItemData item in _listBuilder)
                        {
                            AddToDictionary(item.Item);
                        }
                    }
                    return _dictionaryBuilder;
                }

                private void AddToDictionary(I item)
                {
                    string key = FileUtilities.NormalizePathForComparisonNoThrow(item.EvaluatedInclude, item.ProjectDirectory);

                    if (!_dictionaryBuilder.TryGetValue(key, out var dictionaryValue))
                    {
                        dictionaryValue = new DictionaryValue(item);
                    }
                    else
                    {
                        dictionaryValue.Add(item);
                    }
                    _dictionaryBuilder[key] = dictionaryValue;
                }
            }

            #endregion

            /// <summary>
            /// The list of items in the collection. Defines the enumeration order.
            /// </summary>
            private ImmutableList<ItemData> _list;

            /// <summary>
            /// A dictionary of items keyed by their normalized value.
            /// </summary>
            private ImmutableDictionary<string, DictionaryValue> _dictionary;

            private OrderedItemDataCollection(ImmutableList<ItemData> list, ImmutableDictionary<string, DictionaryValue> dictionary)
            {
                _list = list;
                _dictionary = dictionary;
            }

            /// <summary>
            /// Creates a new mutable collection.
            /// </summary>
            public static Builder CreateBuilder()
            {
                return new Builder(ImmutableList.CreateBuilder<ItemData>(), null);
            }

            /// <summary>
            /// Creates a mutable view of this collection. Changes made to the returned builder are not reflected in this collection.
            /// </summary>
            public Builder ToBuilder()
            {
                return new Builder(_list.ToBuilder(), _dictionary?.ToBuilder());
            }
        }
    }
}
