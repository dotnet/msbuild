// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Build.Framework
{
    internal sealed class MutableTaskItem : ITaskItem2
    {
        private ImmutableDictionary<string, string> _metadata = ImmutableDictionaryExtensions.EmptyMetadata;

        public MutableTaskItem(string itemSpec) => ItemSpec = itemSpec;

        /// <inheritdoc/>
        public string ItemSpec { get; set; }

        /// <inheritdoc/>
        public ICollection MetadataNames => _metadata.Keys.ToList();

        /// <inheritdoc/>
        public int MetadataCount => _metadata.Count;

        /// <inheritdoc/>
        public string GetMetadata(string metadataName) => _metadata.TryGetValue(metadataName, out string? value) ? value : string.Empty;

        /// <inheritdoc/>
        public void SetMetadata(string metadataName, string metadataValue) => _metadata = _metadata.SetItem(metadataName, metadataValue ?? string.Empty);

        /// <inheritdoc/>
        public void RemoveMetadata(string metadataName) => _metadata = _metadata.Remove(metadataName);

        /// <inheritdoc/>
        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            foreach (KeyValuePair<string, string> metadatum in _metadata)
            {
                destinationItem.SetMetadata(metadatum.Key, metadatum.Value);
            }
        }

        /// <inheritdoc/>
        public IDictionary CloneCustomMetadata() => new Dictionary<string, string>(_metadata, _metadata.KeyComparer);

        /// <inheritdoc/>
        public string EvaluatedIncludeEscaped
        {
            get => ItemSpec;
            set => ItemSpec = value;
        }

        /// <inheritdoc/>
        public string GetMetadataValueEscaped(string metadataName) => GetMetadata(metadataName);

        /// <inheritdoc/>
        public void SetMetadataValueLiteral(string metadataName, string metadataValue) => SetMetadata(metadataName, metadataValue);

        /// <inheritdoc/>
        public IDictionary CloneCustomMetadataEscaped() => new Dictionary<string, string>(_metadata, _metadata.KeyComparer);
    }
}
