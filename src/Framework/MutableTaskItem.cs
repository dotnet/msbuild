// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    internal sealed class MutableTaskItem : ITaskItem2
    {
        // MSBuild metadata names are case-insensitive, so the backing store must use a case-insensitive
        // comparer; otherwise GetMetadata("fullpath") and GetMetadata("FullPath") would resolve differently.
        // (The engine's MSBuildNameIgnoreCaseComparer is not available in the Framework assembly, so we use
        // the equivalent ordinal, case-insensitive comparer here.)
        private readonly Dictionary<string, string> _metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MutableTaskItem(string itemSpec) => ItemSpec = itemSpec;

        public string ItemSpec { get; set; }

        public ICollection MetadataNames => _metadata.Keys;

        public int MetadataCount => _metadata.Count;

        public string GetMetadata(string metadataName) => _metadata.TryGetValue(metadataName, out string? value) ? value ?? string.Empty : string.Empty;

        public void SetMetadata(string metadataName, string metadataValue) => _metadata[metadataName] = metadataValue;

        public void RemoveMetadata(string metadataName) => _metadata.Remove(metadataName);

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            foreach (KeyValuePair<string, string> metadatum in _metadata)
            {
                destinationItem.SetMetadata(metadatum.Key, metadatum.Value);
            }
        }

        public IDictionary CloneCustomMetadata() => new Dictionary<string, string>(_metadata);

        public string EvaluatedIncludeEscaped
        {
            get => ItemSpec;
            set => ItemSpec = value;
        }

        public string GetMetadataValueEscaped(string metadataName) => GetMetadata(metadataName);

        public void SetMetadataValueLiteral(string metadataName, string metadataValue) => SetMetadata(metadataName, metadataValue);

        public IDictionary CloneCustomMetadataEscaped() => new Dictionary<string, string>(_metadata);
    }
}
