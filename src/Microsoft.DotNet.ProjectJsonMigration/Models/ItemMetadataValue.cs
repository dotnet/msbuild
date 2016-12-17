// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Models
{
    internal class ItemMetadataValue<T>
    {
        public string MetadataName { get; }
        public string Condition { get; }
        public bool ExpressedAsAttribute { get; }

        private readonly Func<T, string> _metadataValueFunc;
        private readonly Func<T, bool> _writeMetadataConditionFunc;

        public ItemMetadataValue(
            string metadataName,
            string metadataValue,
            string condition = null,
            bool expressedAsAttribute = false) :
                this(metadataName,
                     _ => metadataValue,
                     condition: condition,
                     expressedAsAttribute: expressedAsAttribute)
        {
        }

        public ItemMetadataValue(
            string metadataName,
            Func<T, string> metadataValueFunc,
            Func<T, bool> writeMetadataConditionFunc = null,
            string condition = null,
            bool expressedAsAttribute = false)
        {
            if (metadataName == null)
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            if (metadataValueFunc == null)
            {
                throw new ArgumentNullException(nameof(metadataValueFunc));
            }

            MetadataName = metadataName;
            _metadataValueFunc = metadataValueFunc;
            _writeMetadataConditionFunc = writeMetadataConditionFunc;
            Condition = condition;
            ExpressedAsAttribute = expressedAsAttribute;
        }

        public bool ShouldWriteMetadata(T source)
        {
            return _writeMetadataConditionFunc == null || _writeMetadataConditionFunc(source);
        }

        public string GetMetadataValue(T source)
        {
            return _metadataValueFunc(source);
        }
    }
}
