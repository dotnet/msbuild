// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using EscapingUtilities = Microsoft.Build.Shared.EscapingUtilities;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Implementation of a metadata table for use by the evaluator.
    /// Accumulates ProjectMetadataElement objects and their evaluated value,
    /// overwriting any previous metadata with that name.
    /// </summary>
    internal class EvaluatorMetadataTable : IMetadataTable
    {
        /// <summary>
        /// The actual metadata dictionary.
        /// </summary>
        private Dictionary<string, EvaluatorMetadata>? _metadata;

        /// <summary>
        /// The type of item the metadata should be considered to apply to.
        /// </summary>
        private string _implicitItemType;

        /// <summary>
        /// The expected number of metadata entries in this table.
        /// </summary>
        private readonly int _capacity;

        /// <summary>
        /// Creates a new table using the specified item type.
        /// </summary>
        public EvaluatorMetadataTable(string implicitItemType, int capacity = 0)
        {
            _implicitItemType = implicitItemType;
            _capacity = capacity;
        }

        /// <summary>
        /// Enumerator over the entries in this table
        /// </summary>
        internal IEnumerable<EvaluatorMetadata> Entries => _metadata?.Values ?? Enumerable.Empty<EvaluatorMetadata>();

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name specified,
        /// whatever the item type.
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
        public string GetEscapedValue(string? itemType, string name)
        {
            return GetEscapedValueIfPresent(itemType, name) ?? String.Empty;
        }

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
        /// If no value is available, returns null.
        /// </summary>
        public string? GetEscapedValueIfPresent(string? itemType, string name)
        {
            if (_metadata == null)
            {
                return null;
            }

            string? value = null;

            if (itemType == null || String.Equals(_implicitItemType, itemType, StringComparison.OrdinalIgnoreCase))
            {
                if (_metadata.TryGetValue(name, out EvaluatorMetadata? metadatum))
                {
                    value = metadatum.EvaluatedValueEscaped;
                }
            }

            return value;
        }

        /// <summary>
        /// Adds a metadata entry to the table
        /// </summary>
        internal void SetValue(ProjectMetadataElement xml, string evaluatedValueEscaped)
        {
            if (_metadata == null)
            {
                _metadata = new Dictionary<string, EvaluatorMetadata>(_capacity, MSBuildNameIgnoreCaseComparer.Default);
            }

            _metadata[xml.Name] = new EvaluatorMetadata(xml, evaluatedValueEscaped);
        }

        /// <summary>
        /// An entry in the evaluator's metadata table.
        /// </summary>
        public class EvaluatorMetadata
        {
            /// <summary>
            /// Construct a new EvaluatorMetadata
            /// </summary>
            public EvaluatorMetadata(ProjectMetadataElement xml, string evaluatedValueEscaped)
            {
                this.Xml = xml;
                this.EvaluatedValueEscaped = evaluatedValueEscaped;
            }

            /// <summary>
            /// Gets or sets the metadata Xml
            /// </summary>
            public ProjectMetadataElement Xml
            {
                get;
                private set;
            }

            /// <summary>
            /// Gets or sets the evaluated value, unescaped
            /// </summary>
            public string EvaluatedValue
            {
                get
                {
                    return EscapingUtilities.UnescapeAll(EvaluatedValueEscaped);
                }
            }

            /// <summary>
            /// Gets or sets the evaluated value, escaped as necessary
            /// </summary>
            internal string EvaluatedValueEscaped
            {
                get;
                private set;
            }
        }
    }
}
