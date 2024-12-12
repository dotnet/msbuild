// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A dictionary of unordered property or metadata name/value pairs, with copy-on-write semantics.
    ///
    /// The copy-on-write semantics are only possible if the contained type is immutable, which currently
    /// means it can only be used for ProjectMetadataInstance's.
    /// USE THIS DICTIONARY ONLY FOR IMMUTABLE TYPES. OTHERWISE USE PROPERTYDICTIONARY.
    ///
    /// </summary>
    /// <remarks>
    /// The value that this adds over IDictionary&lt;string, T&gt; is:
    ///     - supports copy on write
    ///     - enforces that key = T.Name
    ///     - default enumerator is over values
    ///     - (marginal) enforces the correct key comparer
    ///
    /// Really a Dictionary&lt;string, T&gt; where the key (the name) is obtained from IKeyed.Key.
    /// Is not observable, so if clients wish to observe modifications they must mediate them themselves and
    /// either not expose this collection or expose it through a readonly wrapper.
    ///
    /// This collection is safe for concurrent readers and a single writer.
    /// </remarks>
    /// <typeparam name="T">Property or Metadata class type to store</typeparam>
    [DebuggerDisplay("#Entries={Count}")]
    internal sealed class CopyOnWritePropertyDictionary<T> : IEnumerable<T>, IEquatable<CopyOnWritePropertyDictionary<T>>, IDictionary<string, T>
        where T : class, IKeyed, IValued, IEquatable<T>, IImmutable
    {
        private static readonly ImmutableDictionary<string, T> NameComparerDictionaryPrototype = ImmutableDictionary.Create<string, T>(MSBuildNameIgnoreCaseComparer.Default);

        /// <summary>
        /// Backing dictionary
        /// </summary>
        private ImmutableDictionary<string, T> _backing;

        /// <summary>
        /// Creates empty dictionary
        /// </summary>
        public CopyOnWritePropertyDictionary()
        {
            _backing = NameComparerDictionaryPrototype;
        }

        /// <summary>
        /// Cloning constructor, with deferred cloning semantics
        /// </summary>
        private CopyOnWritePropertyDictionary(CopyOnWritePropertyDictionary<T> that)
        {
            _backing = that._backing;
        }

        /// <summary>
        /// Accessor for the list of property names
        /// </summary>
        ICollection<string> IDictionary<string, T>.Keys => ((IDictionary<string, T>)_backing).Keys;

        /// <summary>
        /// Accessor for the list of properties
        /// </summary>
        ICollection<T> IDictionary<string, T>.Values => ((IDictionary<string, T>)_backing).Values;

        /// <summary>
        /// Whether the collection is read-only.
        /// </summary>
        bool ICollection<KeyValuePair<string, T>>.IsReadOnly => false;

        /// <summary>
        /// Returns the number of properties in the collection.
        /// </summary>
        public int Count => _backing.Count;

        /// <summary>
        /// Get the property with the specified name, or null if none exists.
        /// Sets the property with the specified name, overwriting it if already exists.
        /// </summary>
        /// <remarks>
        /// Unlike Dictionary&lt;K,V&gt;[K], the getter returns null instead of throwing if the key does not exist.
        /// This better matches the semantics of property, which are considered to have a blank value if they
        /// are not defined.
        /// </remarks>
        public T this[string name]
        {
            get
            {
                // We don't want to check for a zero length name here, since that is a valid name
                // and should return a null instance which will be interpreted as blank
                _backing.TryGetValue(name, out T projectProperty);
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
        /// Returns true if a property with the specified name is present in the collection,
        /// otherwise false.
        /// </summary>
        public bool Contains(string name) => _backing.ContainsKey(name);

        /// <summary>
        /// Empties the collection
        /// </summary>
        public void Clear()
        {
            _backing = _backing.Clear();
        }

        /// <summary>
        /// Gets an enumerator over all the properties in the collection
        /// Enumeration is in undefined order
        /// </summary>
        public IEnumerator<T> GetEnumerator() => _backing.Values.GetEnumerator();

        /// <summary>
        /// Get an enumerator over entries
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #region IEquatable<PropertyDictionary<T>> Members

        /// <summary>
        /// Compares two property dictionaries for equivalence.  They are equal if each contains the same properties with the
        /// same values as the other, unequal otherwise.
        /// </summary>
        /// <param name="other">The dictionary to which this should be compared</param>
        /// <returns>True if they are equivalent, false otherwise.</returns>
        public bool Equals(CopyOnWritePropertyDictionary<T> other)
        {
            if (other == null)
            {
                return false;
            }

            // Copy both backing collections to locals
            ImmutableDictionary<string, T> thisBacking = _backing;
            ImmutableDictionary<string, T> thatBacking = other._backing;

            // If the backing collections are the same, we are equal.
            // Note that with this check, we intentionally avoid the common reference
            // comparison between 'this' and 'other'.
            if (ReferenceEquals(thisBacking, thatBacking))
            {
                return true;
            }

            if (thisBacking.Count != thatBacking.Count)
            {
                return false;
            }

            foreach (T thisProp in thisBacking.Values)
            {
                if (!thatBacking.TryGetValue(thisProp.Key, out T thatProp) ||
                    !EqualityComparer<T>.Default.Equals(thisProp, thatProp))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region IDictionary<string,T> Members

        /// <summary>
        /// Adds a property
        /// </summary>
        void IDictionary<string, T>.Add(string key, T value)
        {
            ErrorUtilities.VerifyThrowInternalNull(value, "Properties can't have null value");
            ErrorUtilities.VerifyThrow(key == value.Key, "Key must match value's key");
            Set(value);
        }

        /// <summary>
        /// Returns true if the dictionary contains the key
        /// </summary>
        bool IDictionary<string, T>.ContainsKey(string key) => _backing.ContainsKey(key);

        /// <summary>
        /// Attempts to retrieve the a property.
        /// </summary>
        bool IDictionary<string, T>.TryGetValue(string key, out T value) => _backing.TryGetValue(key, out value);

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
        /// Checks for a property in the collection
        /// </summary>
        bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> item)
        {
            if (_backing.TryGetValue(item.Key, out T value))
            {
                return EqualityComparer<T>.Default.Equals(value, item.Value);
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
            return Remove(item.Key);
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,T>> Members

        /// <summary>
        /// Get an enumerator over the entries.
        /// </summary>
        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
        {
            return _backing.GetEnumerator();
        }

        #endregion

        /// <summary>
        /// Removes any property with the specified name.
        /// Returns true if the property was in the collection, otherwise false.
        /// </summary>
        public bool Remove(string name)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));

            return ImmutableInterlocked.TryRemove(ref _backing, name, out _);
        }

        /// <summary>
        /// Add the specified property to the collection.
        /// Overwrites any property with the same name already in the collection.
        /// To remove a property, use Remove(...) instead.
        /// </summary>
        internal void Set(T projectProperty)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectProperty, nameof(projectProperty));

            _backing = _backing.SetItem(projectProperty.Key, projectProperty);
        }

        /// <summary>
        /// Adds the specified properties to this dictionary.
        /// </summary>
        /// <param name="other">An enumerator over the properties to add.</param>
        internal void ImportProperties(IEnumerable<T> other)
        {
            _backing = _backing.SetItems(Items());

            IEnumerable<KeyValuePair<string, T>> Items()
            {
                foreach (T property in other)
                {
                    yield return new(property.Key, property);
                }
            }
        }

        /// <summary>
        /// Clone. As we're copy on write, this
        /// should be cheap.
        /// </summary>
        internal CopyOnWritePropertyDictionary<T> DeepClone()
        {
            return new CopyOnWritePropertyDictionary<T>(this);
        }
    }
}
