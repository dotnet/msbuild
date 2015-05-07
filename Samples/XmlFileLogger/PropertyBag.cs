// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// This class encapsulates functionality for a collection of properties (name value pairs) in a
    /// hierarchical way. (e.g. if the parameter is defined and identical in the parent, it is not
    /// stored in this instance). 
    /// </summary>
    internal class PropertyBag
    {
        /// <summary>
        /// The parent instance.
        /// </summary>
        private readonly PropertyBag _parent;

        /// <summary>
        /// The properties defined at this level.
        /// </summary>
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyBag"/> class.
        /// </summary>
        /// <param name="properties">The initial properties to set.</param>
        /// <param name="parent">The parent <see cref="PropertyBag"/> instance.</param>
        public PropertyBag(IEnumerable<KeyValuePair<string, string>> properties, PropertyBag parent = null)
            : this(parent)
        {
            AddProperties(properties);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyBag"/> class.
        /// </summary>
        /// <param name="parent">The parent <see cref="PropertyBag"/>.</param>
        public PropertyBag(PropertyBag parent = null)
        {
            _parent = parent;
        }

        /// <summary>
        /// Gets the properties associated with this instance (without parent properties).
        /// </summary>
        /// <value>
        /// The properties.
        /// </value>
        public IDictionary<string, string> Properties { get { return _properties; } }

        /// <summary>
        /// Adds properties to the collection.
        /// </summary>
        /// <param name="newProperties">The new properties.</param>
        public void AddProperties(IEnumerable<KeyValuePair<string, string>> newProperties)
        {
            if (newProperties == null)
            {
                throw new ArgumentNullException("newProperties");
            }

            foreach (var property in newProperties)
            {
                AddProperty(property.Key, property.Value);
            }
        }

        /// <summary>
        /// Adds properties to the collection.
        /// </summary>
        /// <remarks>If the property is defined and identical in the parent, no action is taken.</remarks>
        /// <param name="newProperties">The new properties.</param>
        /// <exception cref="System.ArgumentException">newProperties</exception>
        public void AddProperties(IEnumerable<DictionaryEntry> newProperties)
        {
            if (newProperties == null)
            {
                throw new ArgumentNullException("newProperties");
            }

            foreach (var property in newProperties)
            {
                AddProperty(property.Key.ToString(), property.Value.ToString());
            }
        }

        /// <summary>
        /// Add a single property to the collection.
        /// </summary>
        /// <remarks>If the property is defined and identical in the parent, no action is taken.</remarks>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        public void AddProperty(string key, string value)
        {
            string currentValue;

            if (_properties.TryGetValue(key, out currentValue))
            {
                // We've already seen the property here, update value if different
                if (currentValue != value)
                {
                    _properties[key] = value;
                }
            }
            else if (_parent != null && _parent.TryGetValue(key, out currentValue))
            {
                // The parent has the property, only add if different
                if (currentValue != value)
                {
                    _properties.Add(key, value);
                }
            }
            else
            {
                // No one has the property, just add it.
                _properties.Add(key, value);
            }
        }

        /// <summary>
        /// Tries the get the value for the property in this scope.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">Out: The property value.</param>
        /// <returns>True if the property was found and set, else false.</returns>
        public bool TryGetValue(string key, out string value)
        {
            if (_properties.TryGetValue(key, out value))
            {
                return true;
            }

            return _parent != null && _parent.TryGetValue(key, out value);
        }
    }
}
