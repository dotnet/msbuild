// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Models
{
    public class ItemMetadataValue<T>
    {
        public string MetadataName { get; }

        private readonly string _metadataValue;
        private readonly Func<T, string> _metadataValueFunc;
        private readonly Func<T, bool> _writeMetadataFunc;

        public ItemMetadataValue(string metadataName, string metadataValue)
        {
            MetadataName = metadataName;
            _metadataValue = metadataValue;
        }

        public ItemMetadataValue(string metadataName, Func<T, string> metadataValueFunc, Func<T, bool> writeMetadataFunc = null)
        {
            MetadataName = metadataName;
            _metadataValueFunc = metadataValueFunc;
            _writeMetadataFunc = writeMetadataFunc;
        }

        public bool ShouldWriteMetadata(T source)
        {
            return _writeMetadataFunc == null || _writeMetadataFunc(source);
        }

        public string GetMetadataValue(T source)
        {
            return _metadataValue ?? _metadataValueFunc(source);
        }
    }
}
