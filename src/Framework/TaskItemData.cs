// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

#nullable disable

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

        /// <summary>
        /// Clone the task item and all metadata to create a snapshot
        /// </summary>
        /// <param name="original">An <see cref="ITaskItem"/> to clone</param>
        public TaskItemData(ITaskItem original)
        {
            ItemSpec = original.ItemSpec;
            var metadata = original.EnumerateMetadata();

            // Can't preallocate capacity because we don't know how large it will get
            // without enumerating the enumerable
            var dictionary = new Dictionary<string, string>();
            foreach (var item in metadata)
            {
                dictionary.Add(item.Key, item.Value);
            }

            Metadata = dictionary;
        }

        SerializableMetadata IMetadataContainer.BackingMetadata => default;

        bool IMetadataContainer.HasCustomMetadata => Metadata.Count > 0;

        IEnumerable<KeyValuePair<string, string>> IMetadataContainer.EnumerateMetadata() => Metadata;

        void IMetadataContainer.ImportMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
            => throw new InvalidOperationException($"{nameof(TaskItemData)} does not support write operations");

        void IMetadataContainer.RemoveMetadataRange(IEnumerable<string> metadataNames) => throw new NotImplementedException();

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
            throw new InvalidOperationException($"{nameof(TaskItemData)} does not support write operations");
        }

        public string GetMetadata(string metadataName)
        {
            Metadata.TryGetValue(metadataName, out var result);
            return result;
        }

        public void RemoveMetadata(string metadataName)
        {
            throw new InvalidOperationException($"{nameof(TaskItemData)} does not support write operations");
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            throw new InvalidOperationException($"{nameof(TaskItemData)} does not support write operations");
        }

        public override string ToString()
        {
            return $"{ItemSpec} Metadata: {MetadataCount}";
        }
    }
}
