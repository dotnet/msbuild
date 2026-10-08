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
                private ItemDataChunkList.Builder _chunkBuilder;

                private ImmutableList<ItemData>.Builder TreeBuilder => _listBuilder ??= ImmutableList.CreateBuilder<ItemData>();

                /// <summary>
                /// A dictionary of items keyed by their normalized value.
                /// </summary>
                private Dictionary<string, ItemDataCollectionValue<I>> _dictionaryBuilder;

                internal Builder(ImmutableList<ItemData>.Builder listBuilder, ItemDataChunkList.Builder chunkBuilder = null)
                {
                    _listBuilder = listBuilder;
                    _chunkBuilder = chunkBuilder;
                }

                #region IEnumerable implementation

                IEnumerator<ItemData> IEnumerable<ItemData>.GetEnumerator() => GetEnumerator();

                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

                private IEnumerator<ItemData> GetEnumerator() => _chunkBuilder is null ? TreeBuilder.GetEnumerator() : _chunkBuilder.GetEnumerator();

                #endregion

                public int Count => _chunkBuilder?.Count ?? _listBuilder?.Count ?? 0;

                private void SetAt(int index, ItemData value)
                {
                    if (_chunkBuilder is null)
                    {
                        TreeBuilder[index] = value;
                    }
                    else
                    {
                        _chunkBuilder[index] = value;
                    }
                }

                private void RemoveWhere(Predicate<ItemData> predicate)
                {
                    if (_chunkBuilder is null)
                    {
                        TreeBuilder.RemoveAll(predicate);
                    }
                    else
                    {
                        _chunkBuilder.RemoveAll(predicate);
                    }
                }

                private void PromoteToChunks(int appendCount)
                {
                    int count = Count;
                    ItemDataChunkList.Builder chunks = ItemDataChunkList.CreateBuilder();
                    chunks.ReserveAppend(count + Math.Min(ItemDataChunkList.ChunkSize, appendCount));
                    if (_listBuilder is not null)
                    {
                        foreach (ItemData data in _listBuilder)
                        {
                            chunks.Add(data);
                        }
                        // Invalidate enumerators of the discarded builder without modifying its frozen snapshots.
                        _listBuilder.Clear();
                    }
                    _listBuilder = null;
                    _chunkBuilder = chunks;
                }

                public ItemData this[int index]
                {
                    get
                    {
                        return _chunkBuilder is null ? TreeBuilder[index] : _chunkBuilder[index];
                    }

                    set
                    {
                        // Update the dictionary if it exists.
                        if (_dictionaryBuilder is not null)
                        {
                            ItemData oldItemData = this[index];
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
                        SetAt(index, value);
                    }
                }

                /// <summary>
                /// Gets or creates a dictionary keyed by normalized values.
                /// </summary>
                public Dictionary<string, ItemDataCollectionValue<I>> Dictionary
                {
                    get
                    {
                        if (_dictionaryBuilder is null)
                        {
                            _dictionaryBuilder = new Dictionary<string, ItemDataCollectionValue<I>>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < Count; i++)
                            {
                                ItemData itemData = this[i];
                                AddToDictionary(ref itemData);
                                SetAt(i, itemData);
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
                    if (_chunkBuilder is null && Count == ItemDataChunkList.ChunkSize - 1)
                    {
                        PromoteToChunks(1);
                    }
                    if (_chunkBuilder is null)
                    {
                        TreeBuilder.Add(data);
                    }
                    else
                    {
                        _chunkBuilder.Add(data);
                    }
                }

                public void ReserveAppend(int count)
                {
                    if (_chunkBuilder is null && count >= ItemDataChunkList.ChunkSize - Count)
                    {
                        PromoteToChunks(count);
                    }
                    _chunkBuilder?.ReserveAppend(count);
                }

                public void Clear()
                {
                    _chunkBuilder?.Clear();
                    _chunkBuilder = null;
                    _listBuilder?.Clear();
                    _dictionaryBuilder?.Clear();
                }

                /// <summary>
                /// Removes all items passed in a collection.
                /// </summary>
                public void RemoveAll(ICollection<I> itemsToRemove)
                {
                    RemoveWhere(item => itemsToRemove.Contains(item.Item));
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
                        RemoveWhere(item => itemsToRemove.Contains(item.Item));
                    }
                }

                /// <summary>
                /// Creates an immutable view of this collection.
                /// </summary>
                public OrderedItemDataCollection ToImmutable()
                {
                    return _chunkBuilder is null
                        ? new OrderedItemDataCollection(_listBuilder?.ToImmutable() ?? ImmutableList<ItemData>.Empty, null)
                        : new OrderedItemDataCollection(null, _chunkBuilder.ToImmutable());
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
            private readonly ImmutableList<ItemData> _list;
            private readonly ItemDataChunkList _chunkList;

            private OrderedItemDataCollection(ImmutableList<ItemData> list, ItemDataChunkList chunkList)
            {
                _list = list;
                _chunkList = chunkList;
            }

            /// <summary>
            /// Creates a new mutable collection.
            /// </summary>
            public static Builder CreateBuilder()
            {
                return new Builder(null);
            }

            /// <summary>
            /// Creates a mutable view of this collection. Changes made to the returned builder are not reflected in this collection.
            /// </summary>
            public Builder ToBuilder()
            {
                return new Builder(_list is { IsEmpty: false } ? _list.ToBuilder() : null, _chunkList?.ToBuilder());
            }

            /// <summary>
            /// Persistent storage for large lists, with copy-on-write arrays and an immutable chunk spine.
            /// Small lists stay in the native tree until they reach one full chunk, and Clear resets that choice.
            /// </summary>
            internal sealed class ItemDataChunkList
            {
                internal const int ChunkSize = 32;
                private readonly ImmutableList<Chunk> _chunks;
                private readonly int _count;

                private ItemDataChunkList(ImmutableList<Chunk> chunks, int count)
                {
                    _chunks = chunks;
                    _count = count;
                }

                internal static Builder CreateBuilder() => new(ImmutableList<Chunk>.Empty.ToBuilder(), 0);

                internal Builder ToBuilder() => new(_chunks.ToBuilder(), _count);

                internal sealed class Chunk
                {
                    internal readonly object Owner;
                    internal readonly ItemData[] Items;

                    internal Chunk(object owner, ItemData[] items)
                    {
                        Owner = owner;
                        Items = items;
                    }
                }

                internal sealed class Builder : IEnumerable<ItemData>
                {
                    private readonly ImmutableList<Chunk>.Builder _chunks;
                    private object _owner;
                    private int _version;
                    private int _remainingAppendCount;

                    internal Builder(ImmutableList<Chunk>.Builder chunks, int count)
                    {
                        _chunks = chunks;
                        Count = count;
                    }

                    internal int Count { get; private set; }

                    internal ItemData this[int index]
                    {
                        get
                        {
                            ValidateIndex(index);
                            return _chunks[index / ChunkSize].Items[index % ChunkSize];
                        }
                        set
                        {
                            ValidateIndex(index);
                            WritableChunk(index / ChunkSize).Items[index % ChunkSize] = value;
                            _version++;
                        }
                    }

                    private void ValidateIndex(int index)
                    {
                        if ((uint)index >= (uint)Count)
                        {
                            throw new ArgumentOutOfRangeException(nameof(index));
                        }
                    }

                    private Chunk WritableChunk(int index, int requiredCapacity = 0)
                    {
                        _owner ??= new object();
                        Chunk chunk = _chunks[index];
                        if (!ReferenceEquals(chunk.Owner, _owner) || chunk.Items.Length < requiredCapacity)
                        {
                            int capacity = requiredCapacity > chunk.Items.Length
                                ? Math.Min(ChunkSize, Math.Max(requiredCapacity, chunk.Items.Length * 2))
                                : chunk.Items.Length;
                            ItemData[] items = new ItemData[capacity];
                            Array.Copy(chunk.Items, items, chunk.Items.Length);
                            chunk = new Chunk(_owner, items);
                            _chunks[index] = chunk;
                        }
                        return chunk;
                    }

                    internal void Add(ItemData item)
                    {
                        int chunkIndex = Count / ChunkSize;
                        int offset = Count % ChunkSize;
                        Chunk chunk;
                        if (offset == 0)
                        {
                            _owner ??= new object();
                            int capacity = Math.Min(ChunkSize, Math.Max(4, _remainingAppendCount));
                            chunk = new Chunk(_owner, new ItemData[capacity]);
                            _chunks.Add(chunk);
                        }
                        else
                        {
                            int capacity = Math.Min(ChunkSize, offset + Math.Max(1, _remainingAppendCount));
                            chunk = WritableChunk(chunkIndex, capacity);
                        }
                        chunk.Items[offset] = item;
                        Count++;
                        if (_remainingAppendCount > 0)
                        {
                            _remainingAppendCount--;
                        }
                        _version++;
                    }

                    internal void ReserveAppend(int count) => _remainingAppendCount = count;

                    internal void Clear()
                    {
                        _chunks.Clear();
                        Count = 0;
                        _remainingAppendCount = 0;
                        _version++;
                    }

                    internal void RemoveAll(Predicate<ItemData> predicate)
                    {
                        _remainingAppendCount = 0;
                        int write = 0;
                        int originalCount = Count;
                        for (int read = 0; read < originalCount; read++)
                        {
                            ItemData item = this[read];
                            if (!predicate(item))
                            {
                                if (write != read)
                                {
                                    this[write] = item;
                                }
                                write++;
                            }
                        }
                        if (write == originalCount)
                        {
                            return;
                        }
                        int requiredChunks = (write + ChunkSize - 1) / ChunkSize;
                        if (write % ChunkSize != 0)
                        {
                            Chunk tail = WritableChunk(requiredChunks - 1);
                            int offset = write % ChunkSize;
                            Array.Clear(tail.Items, offset, tail.Items.Length - offset);
                        }
                        _chunks.RemoveRange(requiredChunks, _chunks.Count - requiredChunks);
                        Count = write;
                        _version++;
                    }

                    internal ItemDataChunkList ToImmutable()
                    {
                        ItemDataChunkList snapshot = new(_chunks.ToImmutable(), Count);
                        // Snapshot publication revokes array ownership without visiting the shared prefix.
                        _owner = null;
                        _remainingAppendCount = 0;
                        return snapshot;
                    }

                    public IEnumerator<ItemData> GetEnumerator() => Enumerate(_version);

                    private IEnumerator<ItemData> Enumerate(int version)
                    {
                        int remaining = Count;
                        foreach (Chunk chunk in _chunks)
                        {
                            int count = Math.Min(ChunkSize, remaining);
                            for (int i = 0; i < count; i++)
                            {
                                if (version != _version)
                                {
                                    throw new InvalidOperationException("Collection was modified during enumeration.");
                                }
                                yield return chunk.Items[i];
                            }
                            remaining -= count;
                        }
                        if (version != _version)
                        {
                            throw new InvalidOperationException("Collection was modified during enumeration.");
                        }
                    }

                    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
        }
    }
}
