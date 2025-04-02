// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Wrapper of two tables for a convenient method return value.
    /// </summary>
    internal struct ItemsAndMetadataPair
    {
        /// <summary>
        /// The item set
        /// </summary>
        private HashSet<string> _items;

        /// <summary>
        /// The metadata dictionary.
        /// The key is the possibly qualified metadata name, for example
        /// "EmbeddedResource.Culture" or "Culture"
        /// </summary>
        private Dictionary<string, MetadataReference> _metadata;

        /// <summary>
        /// Constructs a pair from an item set and metadata
        /// </summary>
        /// <param name="items">The item set</param>
        /// <param name="metadata">The metadata dictionary</param>
        internal ItemsAndMetadataPair(HashSet<string> items, Dictionary<string, MetadataReference> metadata)
        {
            _items = items;
            _metadata = metadata;
        }

        /// <summary>
        /// Gets or sets the item set
        /// </summary>
        internal HashSet<string> Items
        {
            readonly get
            {
                return _items;
            }

            set
            {
                _items = value;
            }
        }

        /// <summary>
        /// Gets or sets the metadata dictionary
        /// The key is the possibly qualified metadata name, for example
        /// "EmbeddedResource.Culture" or "Culture"
        /// </summary>
        internal Dictionary<string, MetadataReference> Metadata
        {
            readonly get
            {
                return _metadata;
            }

            set
            {
                _metadata = value;
            }
        }
    }
}
