// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Execution;
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
    [DebuggerDisplay("#Entries={Count}")]
    internal sealed class CopyOnWritePropertyDictionary : ICopyOnWritePropertyDictionary<ProjectMetadataInstance>, IEquatable<CopyOnWritePropertyDictionary>, IEnumerable<ProjectMetadataInstance>
    {
        private static readonly ImmutableDictionary<string, string> NameComparerDictionaryPrototype = ImmutableDictionary.Create<string, string>(MSBuildNameIgnoreCaseComparer.Default);

        /// <summary>
        /// Backing dictionary.
        /// </summary>
        private ImmutableDictionary<string, string> _backing;

        /// <summary>
        /// Creates empty dictionary.
        /// </summary>
        public CopyOnWritePropertyDictionary() => _backing = NameComparerDictionaryPrototype;

        /// <summary>
        /// Cloning constructor, with deferred cloning semantics.
        /// </summary>
        internal CopyOnWritePropertyDictionary(CopyOnWriteDictionary<string> that) => _backing = that.ToImmutableDictionary();

        /// <summary>
        /// Cloning constructor, with deferred cloning semantics.
        /// </summary>
        private CopyOnWritePropertyDictionary(CopyOnWritePropertyDictionary that) => _backing = that._backing;

        /// <summary>
        /// Accessor for the list of property names.
        /// </summary>
        ICollection<string> IDictionary<string, ProjectMetadataInstance>.Keys => ((IDictionary<string, string>)_backing).Keys;

        /// <summary>
        /// Accessor for the list of properties.
        /// </summary>
        ICollection<ProjectMetadataInstance> IDictionary<string, ProjectMetadataInstance>.Values
        {
            get
            {
                ImmutableDictionary<string, string> backing = _backing;
                int index = 0;
                ProjectMetadataInstance[] values = new ProjectMetadataInstance[backing.Count];

                foreach (KeyValuePair<string, string> entry in backing)
                {
                    values[index] = new ProjectMetadataInstance(entry.Key, entry.Value, allowItemSpecModifiers: true);
                    index++;
                }

                return values;
            }
        }

        /// <summary>
        /// Whether the collection is read-only.
        /// </summary>
        bool ICollection<KeyValuePair<string, ProjectMetadataInstance>>.IsReadOnly => false;

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
        public ProjectMetadataInstance this[string name]
        {
            // We don't want to check for a zero length name here, since that is a valid name
            // and should return a null instance which will be interpreted as blank
            get
            {
                if (_backing.TryGetValue(name, out string escapedValue) && escapedValue != null)
                {
                    return new ProjectMetadataInstance(name, escapedValue, allowItemSpecModifiers: true);
                }

                return null;
            }

            set
            {
                ErrorUtilities.VerifyThrowInternalNull(value, "Properties can't have null value");
                ErrorUtilities.VerifyThrow(string.Equals(name, value.Key, StringComparison.OrdinalIgnoreCase), "Key must match value's key");
                Set(value);
            }
        }

        /// <summary>
        /// Returns true if a property with the specified name is present in the collection,
        /// otherwise false.
        /// </summary>
        public bool Contains(string name) => _backing.ContainsKey(name);

        public string GetEscapedValue(string name) => _backing.TryGetValue(name, out string escapedValue) ? escapedValue : null;

        /// <summary>
        /// Empties the collection
        /// </summary>
        public void Clear() => _backing = _backing.Clear();

        /// <summary>
        /// Gets an enumerator over all the properties in the collection
        /// Enumeration is in undefined order
        /// </summary>
        public IEnumerator<ProjectMetadataInstance> GetEnumerator()
        {
            foreach (KeyValuePair<string, string> item in _backing)
            {
                yield return new ProjectMetadataInstance(item.Key, item.Value, allowItemSpecModifiers: true);
            }
        }

        /// <summary>
        /// Get an enumerator over entries
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #region IEquatable<CopyOnWritePropertyDictionary> Members

        /// <summary>
        /// Compares two property dictionaries for equivalence.  They are equal if each contains the same properties with the
        /// same values as the other, unequal otherwise.
        /// </summary>
        /// <param name="other">The dictionary to which this should be compared</param>
        /// <returns>True if they are equivalent, false otherwise.</returns>
        public bool Equals(CopyOnWritePropertyDictionary other)
        {
            if (other == null)
            {
                return false;
            }

            // Copy both backing collections to locals
            ImmutableDictionary<string, string> thisBacking = _backing;
            ImmutableDictionary<string, string> thatBacking = other._backing;

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

            foreach (KeyValuePair<string, string> thisEntry in thisBacking)
            {
                if (!thatBacking.TryGetValue(thisEntry.Key, out string thatValue) || thisEntry.Value != thatValue)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region IEquatable<CopyOnWritePropertyDictionary<ProjectMetadataInstance>> Members

        /// <summary>
        /// Compares two property dictionaries for equivalence.  They are equal if each contains the same properties with the
        /// same values as the other, unequal otherwise.
        /// </summary>
        /// <param name="other">The dictionary to which this should be compared</param>
        /// <returns>True if they are equivalent, false otherwise.</returns>
        public bool Equals(ICopyOnWritePropertyDictionary<ProjectMetadataInstance> other)
        {
            if (other == null)
            {
                return false;
            }

            ImmutableDictionary<string, string> thisBacking = _backing;

            if (other is CopyOnWritePropertyDictionary otherCopyOnWritePropertyDictionary)
            {
                return Equals(otherCopyOnWritePropertyDictionary);
            }

            if (thisBacking.Count != other.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> thisEntry in thisBacking)
            {
                if (!other.TryGetValue(thisEntry.Key, out ProjectMetadataInstance thatProp) ||
                    !EqualityComparer<ProjectMetadataInstance>.Default.Equals(new ProjectMetadataInstance(thisEntry.Key, thisEntry.Value, allowItemSpecModifiers: true), thatProp))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region IDictionary<string, ProjectMetadataInstance> Members

        /// <summary>
        /// Adds a property
        /// </summary>
        void IDictionary<string, ProjectMetadataInstance>.Add(string key, ProjectMetadataInstance value)
        {
            ErrorUtilities.VerifyThrowInternalNull(value, "Properties can't have null value");
            ErrorUtilities.VerifyThrow(key == value.Key, "Key must match value's key");
            Set(value);
        }

        /// <summary>
        /// Returns true if the dictionary contains the key
        /// </summary>
        bool IDictionary<string, ProjectMetadataInstance>.ContainsKey(string key) => _backing.ContainsKey(key);

        /// <summary>
        /// Attempts to retrieve the a property.
        /// </summary>
        bool IDictionary<string, ProjectMetadataInstance>.TryGetValue(string key, out ProjectMetadataInstance value)
        {
            value = null;

            if (_backing.TryGetValue(key, out string escapedValue))
            {
                if (escapedValue != null)
                {
                    value = new ProjectMetadataInstance(key, escapedValue, allowItemSpecModifiers: true);
                }

                return true;
            }

            return false;
        }

        #endregion

        #region ICollection<KeyValuePair<string,ProjectMetadataInstance>> Members

        /// <summary>
        /// Adds a property
        /// </summary>
        void ICollection<KeyValuePair<string, ProjectMetadataInstance>>.Add(KeyValuePair<string, ProjectMetadataInstance> item)
        {
            ((IDictionary<string, ProjectMetadataInstance>)this).Add(item.Key, item.Value);
        }

        /// <summary>
        /// Checks for a property in the collection
        /// </summary>
        bool ICollection<KeyValuePair<string, ProjectMetadataInstance>>.Contains(KeyValuePair<string, ProjectMetadataInstance> item)
        {
            if (_backing.TryGetValue(item.Key, out string escapedValue))
            {
                return EqualityComparer<ProjectMetadataInstance>.Default.Equals(new ProjectMetadataInstance(item.Key, escapedValue, allowItemSpecModifiers: true), item.Value);
            }

            return false;
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        void ICollection<KeyValuePair<string, ProjectMetadataInstance>>.CopyTo(KeyValuePair<string, ProjectMetadataInstance>[] array, int arrayIndex)
        {
            ErrorUtilities.ThrowInternalError("CopyTo is not supported on PropertyDictionary.");
        }

        /// <summary>
        /// Removes a property from the collection
        /// </summary>
        bool ICollection<KeyValuePair<string, ProjectMetadataInstance>>.Remove(KeyValuePair<string, ProjectMetadataInstance> item)
        {
            ErrorUtilities.VerifyThrow(item.Key == item.Value.Key, "Key must match value's key");
            return Remove(item.Key);
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,ProjectMetadataInstance>> Members

        /// <summary>
        /// Get an enumerator over the entries.
        /// </summary>
        IEnumerator<KeyValuePair<string, ProjectMetadataInstance>> IEnumerable<KeyValuePair<string, ProjectMetadataInstance>>.GetEnumerator()
        {
            foreach (KeyValuePair<string, string> item in _backing)
            {
                ProjectMetadataInstance projectProperty = new(item.Key, item.Value, allowItemSpecModifiers: true);
                yield return new(item.Key, projectProperty);
            }
        }

        #endregion

        /// <summary>
        /// Removes any property with the specified name.
        /// Returns true if the property was in the collection, otherwise false.
        /// </summary>
        public bool Remove(string name)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name);

            return ImmutableInterlocked.TryRemove(ref _backing, name, out _);
        }

        /// <summary>
        /// Add the specified property to the collection.
        /// Overwrites any property with the same name already in the collection.
        /// To remove a property, use Remove(...) instead.
        /// </summary>
        public void Set(ProjectMetadataInstance projectProperty)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectProperty);

            _backing = _backing.SetItem(projectProperty.Key, projectProperty.EscapedValue);
        }

        /// <summary>
        /// Adds the specified properties to this dictionary.
        /// </summary>
        /// <param name="other">An enumerator over the properties to add.</param>
        public void ImportProperties(IEnumerable<ProjectMetadataInstance> other)
        {
            _backing = _backing.SetItems(Items(other));

            static IEnumerable<KeyValuePair<string, string>> Items(IEnumerable<ProjectMetadataInstance> other)
            {
                foreach (ProjectMetadataInstance property in other)
                {
                    yield return new(property.Key, property.EscapedValue);
                }
            }
        }

        /// <summary>
        /// Clone. As we're copy on write, this
        /// should be cheap.
        /// </summary>
        public ICopyOnWritePropertyDictionary<ProjectMetadataInstance> DeepClone() => new CopyOnWritePropertyDictionary(this);

        /// <summary>
        /// Returns true if these dictionaries have the same backing.
        /// </summary>
        public bool HasSameBacking(ICopyOnWritePropertyDictionary<ProjectMetadataInstance> other)
        {
            return other is CopyOnWritePropertyDictionary otherCopyOnWritePropertyDictionary
                ? ReferenceEquals(otherCopyOnWritePropertyDictionary._backing, _backing)
                : false;
        }

        internal CopyOnWriteDictionary<string> ToCopyOnWriteDictionary() => new(_backing);
    }
}
