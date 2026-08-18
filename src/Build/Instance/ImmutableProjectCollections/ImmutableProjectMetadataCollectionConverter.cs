// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    internal class ImmutableProjectMetadataCollectionConverter : IReadOnlyDictionary<string, string>
    {
        /// <summary>
        /// Immutable project item.
        /// </summary>
        private readonly ProjectItem _linkedProjectItem;

        /// <summary>
        /// Properties in the underlying dictionary.
        /// This dictionary contains all properties that are directly defined in the project item.
        /// </summary>
        private readonly IDictionary<string, ProjectMetadata> _properties;

        /// <summary>
        /// A cached immutable dictionary containing all direct properties.
        /// </summary>
        private ImmutableDictionary<string, string>? _convertedPropertiesDictionary;

        public ImmutableProjectMetadataCollectionConverter(
            ProjectItem linkedProjectItem,
            IDictionary<string, ProjectMetadata> properties)
        {
            _linkedProjectItem = linkedProjectItem ?? throw new ArgumentNullException(nameof(linkedProjectItem));
            _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public IEnumerable<string> Keys => _properties.Keys;

        public IEnumerable<string> Values => _properties.Values.Select(m => m.EvaluatedValueEscaped);

        public int Count => _properties.Count;

        public string this[string key]
        {
            get
            {
                if (_properties.ContainsKey(key))
                {
                    return EscapingUtilities.Escape(_linkedProjectItem.GetMetadataValue(key));
                }

                throw new KeyNotFoundException($"The metadata '{key}' does not exist in the project item '{_linkedProjectItem.ItemType}'.");
            }

            set => throw new NotSupportedException("Cannot set value in an immutable collection.");
        }

        public bool ContainsKey(string key) => _properties.ContainsKey(key);

        public bool TryGetValue(string key, out string value)
        {
            if (ContainsKey(key))
            {
                value = EscapingUtilities.Escape(_linkedProjectItem.GetMetadataValue(key));
                return true;
            }

            value = null!;
            return false;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (string name in _properties.Keys)
            {
                yield return new KeyValuePair<string, string>(
                    name,
                    EscapingUtilities.Escape(_linkedProjectItem.GetMetadataValue(name)));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Gets value of any property in the project item. (including direct properties, properties from item type definitions and built-in properties.)
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>Unescaped value of the property, or an empty string.</returns>
        public string GetExtendedPropertyValue(string name) => _linkedProjectItem.GetMetadataValue(name);

        /// <summary>
        /// Converts the collection to an immutable dictionary.
        /// </summary>
        /// <returns>An immutable dictionary containing all direct properties.</returns>
        public ImmutableDictionary<string, string> ToImmutableDictionary()
        {
            if (_convertedPropertiesDictionary is null)
            {
                var newDictionary = _properties.ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp => EscapingUtilities.Escape(_linkedProjectItem.GetMetadataValue(kvp.Key)),
                    MSBuildNameIgnoreCaseComparer.Default);

                _ = Interlocked.CompareExchange(ref _convertedPropertiesDictionary, newDictionary, null);
            }

            return _convertedPropertiesDictionary;
        }
    }
}
