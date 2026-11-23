// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation
{
    internal readonly struct EvaluatedMetadataEntry
    {
        internal ProjectMetadataElement Element { get; }
        internal string Name { get; }
        internal string EscapedValue { get; }

        internal EvaluatedMetadataEntry(ProjectMetadataElement element, string name, string escapedValue)
        {
            Element = element;
            Name = name;
            EscapedValue = escapedValue;
        }
    }

    /// <summary>
    /// Stores evaluated metadata in declaration order and creates a lookup table only if prefix metadata is read.
    /// </summary>
    internal sealed class EvaluatedMetadataTable : IMetadataTable
    {
        private readonly string _implicitItemType;
        private readonly EvaluatedMetadataEntry[] _entries;
        private Dictionary<string, string>? _metadata;

        internal EvaluatedMetadataTable(string implicitItemType, int capacity)
        {
            _implicitItemType = implicitItemType;
            _entries = capacity == 0 ? Array.Empty<EvaluatedMetadataEntry>() : new EvaluatedMetadataEntry[capacity];
        }

        internal int Count { get; private set; }

        internal EvaluatedMetadataEntry this[int index] => _entries[index];

        public string GetEscapedValue(string name)
        {
            return GetEscapedValue(null, name);
        }

        public string GetEscapedValue(string? itemType, string name)
        {
            return GetEscapedValueIfPresent(itemType, name) ?? String.Empty;
        }

        public string? GetEscapedValueIfPresent(string? itemType, string name)
        {
            if (itemType is not null && !String.Equals(_implicitItemType, itemType, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (Count == 0)
            {
                return null;
            }

            EnsureMetadataTable();
            return _metadata!.TryGetValue(name, out string? value) ? value : null;
        }

        internal void SetValue(ProjectMetadataElement xml, string evaluatedValueEscaped)
        {
            string name = xml.Name;
            if (_metadata is not null)
            {
                _metadata[name] = evaluatedValueEscaped;
            }

            _entries[Count++] = new EvaluatedMetadataEntry(xml, name, evaluatedValueEscaped);
        }

        private void EnsureMetadataTable()
        {
            if (_metadata is not null)
            {
                return;
            }

            _metadata = new Dictionary<string, string>(Count, MSBuildNameIgnoreCaseComparer.Default);
            for (int i = 0; i < Count; i++)
            {
                EvaluatedMetadataEntry entry = _entries[i];
                _metadata[entry.Name] = entry.EscapedValue;
            }
        }
    }
}
