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

        public ItemMetadataValue(string metadataName, string metadataValue)
        {
            MetadataName = metadataName;
            _metadataValue = metadataValue;
        }

        public ItemMetadataValue(string metadataName, Func<T, string> metadataValueFunc)
        {
            MetadataName = metadataName;
            _metadataValueFunc = metadataValueFunc;
        }

        public string GetMetadataValue(T source)
        {
            return _metadataValue ?? _metadataValueFunc(source);
        }
    }
}
