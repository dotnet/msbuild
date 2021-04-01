// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Lightweight specialized implementation of <see cref="ITaskItem"/> only used for deserializing items.
    /// The goal is to minimize overhead when representing deserialized items.
    /// Used by node packet translator and binary logger.
    /// </summary>
    internal class TaskItemData : ITaskItem, IMetadataContainer
    {
        private static readonly Dictionary<string, string> _emptyMetadata = new Dictionary<string, string>();

        public string ItemSpec { get; set; }
        public IDictionary<string, string> Metadata { get; }

        public TaskItemData(string itemSpec, IDictionary<string, string> metadata)
        {
            ItemSpec = itemSpec;
            Metadata = metadata ?? _emptyMetadata;
        }

        IEnumerable<KeyValuePair<string, string>> IMetadataContainer.EnumerateMetadata() => Metadata;

        public int MetadataCount => Metadata.Count;

        public ICollection MetadataNames => (ICollection)Metadata.Keys;

        public IDictionary CloneCustomMetadata()
        {
            // against the guidance for CloneCustomMetadata this returns the original collection.
            // Since this is only to be used for serialization and logging, consumers should not
            // modify the collection. We need to minimize allocations so avoid cloning here.
            return (IDictionary)Metadata;
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            throw new NotImplementedException();
        }

        public string GetMetadata(string metadataName)
        {
            Metadata.TryGetValue(metadataName, out var result);
            return result;
        }

        public void RemoveMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"{ItemSpec} Metadata: {MetadataCount}";
        }
    }
}
