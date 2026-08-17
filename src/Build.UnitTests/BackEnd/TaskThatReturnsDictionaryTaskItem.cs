// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests;
/// <summary>
/// Task that returns a custom ITaskItem implementation that has a custom IDictionary type returned from CloneCustomMetadata()
/// </summary>
public sealed class TaskThatReturnsDictionaryTaskItem : Utilities.Task
{
    public string Key { get; set; }
    public string Value { get; set; }

    public override bool Execute()
    {
        var metaValue = new MinimalDictionary<string, string>
        {
            { Key, Value }
        };
        DictionaryTaskItemOutput = new MinimalDictionaryTaskItem(metaValue);
        return true;
    }

    [Output]
    public ITaskItem DictionaryTaskItemOutput { get; set; }

    internal sealed class MinimalDictionaryTaskItem : ITaskItem
    {
        private MinimalDictionary<string, string> _metaData = new MinimalDictionary<string, string>();

        public MinimalDictionaryTaskItem(MinimalDictionary<string, string> metaValue)
        {
            _metaData = metaValue;
        }

        public string ItemSpec { get => $"{nameof(MinimalDictionaryTaskItem)}spec"; set => throw new NotImplementedException(); }

        public ICollection MetadataNames => throw new NotImplementedException();

        public int MetadataCount => throw new NotImplementedException();

        ICollection ITaskItem.MetadataNames => throw new NotImplementedException();

        public IDictionary CloneCustomMetadata() => _metaData;

        public string GetMetadata(string metadataName)
        {
            if (String.IsNullOrEmpty(metadataName))
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            string value = (string)_metaData[metadataName];
            return value;
        }

        public void SetMetadata(string metadataName, string metadataValue) => throw new NotImplementedException();
        public void RemoveMetadata(string metadataName) => throw new NotImplementedException();
        public void CopyMetadataTo(ITaskItem destinationItem) => throw new NotImplementedException();
    }
}

public sealed class MinimalDictionary<TKey, TValue> : IDictionary
{
    private List<TKey> _keys = new List<TKey>();
    private List<TValue> _values = new List<TValue>();

    public object this[object key]
    {
        get
        {
            int index = _keys.IndexOf((TKey)key);
            return index == -1 ? throw new KeyNotFoundException() : (object)_values[index];
        }
        set
        {
            int index = _keys.IndexOf((TKey)key);
            if (index == -1)
            {
                _keys.Add((TKey)key);
                _values.Add((TValue)value);
            }
            else
            {
                _values[index] = (TValue)value;
            }
        }
    }

    public bool IsFixedSize => false;

    public bool IsReadOnly => false;

    public ICollection Keys => _keys;

    public ICollection Values => _values;

    public int Count => _keys.Count;

    public bool IsSynchronized => false;

    public object SyncRoot => throw new NotSupportedException();

    public void Add(object key, object value)
    {
        if (_keys.Contains((TKey)key))
        {
            throw new ArgumentException("An item with the same key has already been added.");
        }

        _keys.Add((TKey)key);
        _values.Add((TValue)value);
    }

    public void Clear()
    {
        _keys.Clear();
        _values.Clear();
    }

    public bool Contains(object key)
    {
        return _keys.Contains((TKey)key);
    }

    public void CopyTo(Array array, int index)
    {
        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (array.Rank != 1)
        {
            throw new ArgumentException("Array must be one-dimensional.", nameof(array));
        }

        if (index < 0 || index > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (array.Length - index < Count)
        {
            throw new ArgumentException("The number of elements in the source is greater than the available space from index to the end of the destination array.");
        }

        for (int i = 0; i < Count; i++)
        {
            array.SetValue(new KeyValuePair<TKey, TValue>(_keys[i], _values[i]), index + i);
        }
    }

    public IDictionaryEnumerator GetEnumerator() => new MinimalDictionaryEnumerator(_keys, _values);

    public void Remove(object key)
    {
        int index = _keys.IndexOf((TKey)key);
        if (index != -1)
        {
            _keys.RemoveAt(index);
            _values.RemoveAt(index);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return new KeyValuePair<TKey, TValue>(_keys[i], _values[i]);
        }
    }

    private sealed class MinimalDictionaryEnumerator : IDictionaryEnumerator
    {
        private List<TKey> _keys;
        private List<TValue> _values;
        private int _index = -1;

        public MinimalDictionaryEnumerator(List<TKey> keys, List<TValue> values)
        {
            _keys = keys;
            _values = values;
        }

        public object Current => Entry;

        public object Key => _keys[_index];

        public object Value => _values[_index];

        public DictionaryEntry Entry => new DictionaryEntry(Key, Value);

        public bool MoveNext()
        {
            return ++_index < _keys.Count;
        }

        public void Reset()
        {
            _index = -1;
        }
    }
}
