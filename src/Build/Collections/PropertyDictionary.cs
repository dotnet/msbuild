﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A dictionary of unordered property or metadata name/value pairs.
    /// </summary>
    /// <remarks>
    /// The value that this adds over IDictionary&lt;string, T&gt; is:
    ///     - enforces that key = T.Name
    ///     - default enumerator is over values
    ///     - (marginal) enforces the correct key comparer
    ///     - potentially makes copy on write possible
    /// 
    /// Really a Dictionary&lt;string, T&gt; where the key (the name) is obtained from IKeyed.Key.
    /// Is not observable, so if clients wish to observe modifications they must mediate them themselves and 
    /// either not expose this collection or expose it through a readonly wrapper.
    /// At various places in this class locks are taken on the backing collection.  The reason for this is to allow
    /// this class to be asynchronously enumerated.  This is accomplished by the CopyOnReadEnumerable which will 
    /// lock the backing collection when it does its deep cloning.  This prevents asynchronous access from corrupting
    /// the state of the enumeration until the collection has been fully copied.
    /// 
    /// Since we use the mutable ignore case comparer we need to make sure that we lock our self before we call the comparer since the comparer can call back 
    /// into this dictionary which could cause a deadlock if another thread is also accessing another method in the dictionary.
    /// </remarks>
    /// <typeparam name="T">Property or Metadata class type to store</typeparam>
    [DebuggerDisplay("#Entries={Count}")]
    internal sealed class PropertyDictionary<T> : IEnumerable<T>, IEquatable<PropertyDictionary<T>>, IPropertyProvider<T>, IDictionary<string, T>
        where T : class, IKeyed, IValued, IEquatable<T>
    {
        /// <summary>
        /// Backing dictionary
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private readonly RetrievableEntryHashSet<T> _properties;

        /// <summary>
        /// Creates empty dictionary
        /// </summary>
        public PropertyDictionary()
        {
            _properties = new RetrievableEntryHashSet<T>(MSBuildNameIgnoreCaseComparer.Default);
        }

        /// <summary>
        /// Creates empty dictionary, optionally specifying initial capacity
        /// </summary>
        internal PropertyDictionary(int capacity)
        {
            _properties = new RetrievableEntryHashSet<T>(capacity, MSBuildNameIgnoreCaseComparer.Default);
        }

        /// <summary>
        /// Create a new dictionary from an enumerator
        /// </summary>
        internal PropertyDictionary(IEnumerable<T> elements)
            : this()
        {
            foreach (T element in elements)
            {
                Set(element);
            }
        }

        /// <summary>
        /// Creates empty dictionary, specifying a comparer
        /// </summary>
        internal PropertyDictionary(MSBuildNameIgnoreCaseComparer comparer)
        {
            _properties = new RetrievableEntryHashSet<T>(comparer);
        }

        /// <summary>
        /// Create a new dictionary from an enumerator
        /// </summary>
        internal PropertyDictionary(int capacity, IEnumerable<T> elements)
            : this(capacity)
        {
            foreach (T element in elements)
            {
                Set(element);
            }
        }

        /// <summary>
        /// Accessor for the list of property names
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ICollection<string> IDictionary<string, T>.Keys
        {
            get
            {
                ErrorUtilities.ThrowInternalError("Keys is not supported on PropertyDictionary.");

                // Show the compiler that this always throws:
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Accessor for the list of properties
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ICollection<T> IDictionary<string, T>.Values
        {
            get
            {
                lock (_properties)
                {
                    return _properties.Values;
                }
            }
        }

        /// <summary>
        /// Returns the number of properties in the collection
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int ICollection<KeyValuePair<string, T>>.Count
        {
            get
            {
                lock (_properties)
                {
                    return _properties.Count;
                }
            }
        }

        /// <summary>
        /// Whether the collection is read-only.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ICollection<KeyValuePair<string, T>>.IsReadOnly => false;

        /// <summary>
        /// Returns the number of property in the collection.
        /// </summary>
        internal int Count
        {
            get
            {
                lock (_properties)
                {
                    return _properties.Count;
                }
            }
        }

        /// <summary>
        /// Get the property with the specified name, or null if none exists.
        /// Sets the property with the specified name, overwriting it if already exists.
        /// </summary>
        /// <remarks>
        /// Unlike Dictionary&lt;K,V&gt;[K], the getter returns null instead of throwing if the key does not exist.
        /// This better matches the semantics of property, which are considered to have a blank value if they
        /// are not defined.
        /// </remarks>
        T IDictionary<string, T>.this[string name]
        {
            get => this[name];
            set => this[name] = value;
        }

        /// <summary>
        /// Get the property with the specified name, or null if none exists.
        /// Sets the property with the specified name, overwriting it if already exists.
        /// </summary>
        /// <remarks>
        /// Unlike Dictionary&lt;K,V&gt;[K], the getter returns null instead of throwing if the key does not exist.
        /// This better matches the semantics of property, which are considered to have a blank value if they
        /// are not defined.
        /// </remarks>
        internal T this[string name]
        {
            get
            {
                // We don't want to check for a zero length name here, since that is a valid name
                // and should return a null instance which will be interpreted as blank
                T projectProperty;
                lock (_properties)
                {
                    _properties.TryGetValue(name, out projectProperty);
                }

                return projectProperty;
            }

            set
            {
                ErrorUtilities.VerifyThrowInternalNull(value, "Properties can't have null value");
                ErrorUtilities.VerifyThrow(String.Equals(name, value.Key, StringComparison.OrdinalIgnoreCase), "Key must match value's key");
                Set(value);
            }
        }

        /// <summary>
        /// Returns an enumerable which copies the underlying data on read.
        /// </summary>
        public IEnumerable<TResult> GetCopyOnReadEnumerable<TResult>(Func<T, TResult> selector)
        {
            return new CopyOnReadEnumerable<T, TResult>(this, _properties, selector);
        }

        /// <summary>
        /// Returns true if a property with the specified name is present in the collection,
        /// otherwise false.
        /// </summary>
        public bool Contains(string name)
        {
            return ((IDictionary<string, T>)this).ContainsKey(name);
        }

        /// <summary>
        /// Empties the collection
        /// </summary>
        public void Clear()
        {
            lock (_properties)
            {
                _properties.Clear();
            }
        }

        /// <summary>
        /// Gets an enumerator over all the properties in the collection
        /// Enumeration is in undefined order
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            lock (_properties)
            {
                return _properties.Values.GetEnumerator();
            }
        }

        /// <summary>
        /// Get an enumerator over entries
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (_properties)
            {
                return ((IEnumerable)_properties.Values).GetEnumerator();
            }
        }

        #region IEquatable<PropertyDictionary<T>> Members

        /// <summary>
        /// Compares two property dictionaries for equivalence.  They are equal if each contains the same properties with the
        /// same values as the other, unequal otherwise.
        /// </summary>
        /// <param name="other">The dictionary to which this should be compared</param>
        /// <returns>True if they are equivalent, false otherwise.</returns>
        public bool Equals(PropertyDictionary<T> other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Count != other.Count)
            {
                return false;
            }

            lock (_properties)
            {
                foreach (T leftProp in this)
                {
                    T rightProp = other[leftProp.Key];
                    if (rightProp?.Equals(leftProp) != true)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        /// <summary>
        /// Get the property with the specified name or null if it is not present
        /// </summary>
        public T GetProperty(string name)
        {
            // The properties lock is locked in indexor
            return this[name];
        }

        /// <summary>
        /// Get the property with the specified name or null if it is not present.
        /// Name is the segment of the provided string with the provided start and end indexes.
        /// </summary>
        public T GetProperty(string name, int startIndex, int endIndex)
        {
            lock (_properties)
            {
                return _properties.Get(name, startIndex, endIndex - startIndex + 1);
            }
        }

        #region IDictionary<string,T> Members

        /// <summary>
        /// Adds a property
        /// </summary>
        void IDictionary<string, T>.Add(string key, T value)
        {
            ErrorUtilities.VerifyThrow(key == value.Key, "Key must match value's key");

            // The properties lock is locked in the set method
            Set(value);
        }

        /// <summary>
        /// Returns true if the dictionary contains the key
        /// </summary>
        bool IDictionary<string, T>.ContainsKey(string key)
        {
            lock (_properties)
            {
                return _properties.ContainsKey(key);
            }
        }

        /// <summary>
        /// Removes a property
        /// </summary>
        bool IDictionary<string, T>.Remove(string key)
        {
            return Remove(key);
        }

        /// <summary>
        /// Attempts to retrieve the a property.
        /// </summary>
        bool IDictionary<string, T>.TryGetValue(string key, out T value)
        {
            value = this[key];

            return value != null;
        }

        #endregion

        #region ICollection<KeyValuePair<string,T>> Members

        /// <summary>
        /// Adds a property
        /// </summary>
        void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> item)
        {
            ((IDictionary<string, T>)this).Add(item.Key, item.Value);
        }

        /// <summary>
        /// Clears the property collection
        /// </summary>
        void ICollection<KeyValuePair<string, T>>.Clear()
        {
            Clear();
        }

        /// <summary>
        /// Checks for a property in the collection
        /// </summary>
        bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> item)
        {
            lock (_properties)
            {
                if (_properties.TryGetValue(item.Key, out T value))
                {
                    return ReferenceEquals(value, item.Value);
                }
            }

            return false;
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        void ICollection<KeyValuePair<string, T>>.CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
        {
            ErrorUtilities.ThrowInternalError("CopyTo is not supported on PropertyDictionary.");
        }

        /// <summary>
        /// Removes a property from the collection
        /// </summary>
        bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> item)
        {
            ErrorUtilities.VerifyThrow(item.Key == item.Value.Key, "Key must match value's key");

            // The properties lock is locked in the remove method
            return ((IDictionary<string, T>)this).Remove(item.Key);
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,T>> Members

        /// <summary>
        /// Get an enumerator over the entries.
        /// </summary>
        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
        {
            lock (_properties)
            {
                foreach (var entry in _properties)
                {
                    yield return new KeyValuePair<string, T>(entry.Key, entry);
                }
            }
        }

        #endregion

        /// <summary>
        /// Removes any property with the specified name.
        /// Returns true if the property was in the collection, otherwise false.
        /// </summary>
        internal bool Remove(string name)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));

            lock (_properties)
            {
                bool result = _properties.Remove(name);
                return result;
            }
        }

        /// <summary>
        /// Add the specified property to the collection.
        /// Overwrites any property with the same name already in the collection.
        /// To remove a property, use Remove(...) instead.
        /// </summary>
        internal void Set(T projectProperty)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectProperty, nameof(projectProperty));

            lock (_properties)
            {
                _properties[projectProperty.Key] = projectProperty;
            }
        }

        /// <summary>
        /// Adds the specified properties to this dictionary.
        /// </summary>
        /// <param name="other">An enumerator over the properties to add.</param>
        internal void ImportProperties(IEnumerable<T> other)
        {
            // The properties lock is locked in the set method
            foreach (T property in other)
            {
                Set(property);
            }
        }

        /// <summary>
        /// Removes the specified properties from this dictionary
        /// </summary>
        /// <param name="other">An enumerator over the properties to remove.</param>
        internal void RemoveProperties(IEnumerable<T> other)
        {
            // The properties lock is locked in the set method
            foreach (T property in other)
            {
                Remove(property.Key);
            }
        }

        /// <summary>
        /// Helper to convert into a read-only dictionary of string, string.
        /// TODO: for performance, consider switching to returning IDictionary
        /// and returning ArrayDictionary if lookup of results is not needed.
        /// </summary>
        internal Dictionary<string, string> ToDictionary()
        {
            lock (_properties)
            {
                var dictionary = new Dictionary<string, string>(_properties.Count, MSBuildNameIgnoreCaseComparer.Default);

                foreach (T property in this)
                {
                    dictionary[property.Key] = property.EscapedValue;
                }

                return dictionary;
            }
        }

        internal void Enumerate(Action<string, string> keyValueCallback)
        {
            lock (_properties)
            {
                foreach (var kvp in _properties)
                {
                    keyValueCallback(kvp.Key, EscapingUtilities.UnescapeAll(kvp.EscapedValue));
                }
            }
        }

        internal IEnumerable<TResult> Filter<TResult>(Func<T, bool> filter, Func<T, TResult> selector)
        {
            List<TResult> result = new();
            lock (_properties)
            {
                foreach (T property in _properties)
                {
                    if (filter(property))
                    {
                        result.Add(selector(property));
                    }
                }
            }

            return result;
        }
    }
}
