﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Wraps a table of metadata values in which keys
    /// may be qualified ("itemtype.name") or unqualified ("name").
    /// </summary>
    internal class StringMetadataTable : IMetadataTable
    {
        /// <summary>
        /// Table of metadata values. 
        /// Each key may be qualified ("itemtype.name") or unqualified ("name").
        /// Unqualified are considered to apply to all item types.
        /// May be null, if empty.
        /// </summary>
        private Dictionary<string, string> _metadata;

        /// <summary>
        /// Constructor taking a table of metadata in which keys
        /// may be a mixture of qualified ("itemtype.name") and unqualified ("name").
        /// Unqualified keys are considered to apply to all item types.
        /// Metadata may be null, indicating it is empty.
        /// </summary>
        internal StringMetadataTable(Dictionary<string, string> metadata)
        {
            _metadata = metadata;
        }

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name specified.
        /// If no value is available, returns empty string.
        /// </summary>
        public string GetEscapedValue(string name)
        {
            return GetEscapedValue(null, name);
        }

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
        /// If no value is available, returns empty string.
        /// </summary>
        public string GetEscapedValue(string itemType, string name)
        {
            return GetEscapedValueIfPresent(itemType, name) ?? String.Empty;
        }

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
        /// If no value is available, returns null.
        /// </summary>
        public string GetEscapedValueIfPresent(string itemType, string name)
        {
            if (_metadata == null)
            {
                return null;
            }

            string key;
            if (itemType == null)
            {
                key = name;
            }
            else
            {
                key = itemType + "." + name;
            }

            string value;
            _metadata.TryGetValue(key, out value);

            return value;
        }
    }
}
