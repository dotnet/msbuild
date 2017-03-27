// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Models;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    internal class RemoveItemTransform<T> : ConditionalTransform<T, ProjectItemElement>
    {
        private readonly ProjectRootElement _itemObjectGenerator = ProjectRootElement.Create();

        private readonly string _itemName;
        private readonly Func<T, string> _removeValueFunc;

        private readonly List<ItemMetadataValue<T>> _metadata = new List<ItemMetadataValue<T>>();

        public RemoveItemTransform(
            string itemName,
            Func<T, string> removeValueFunc,
            Func<T, bool> condition)
            : base(condition)
        {
            _itemName = itemName;
            _removeValueFunc = removeValueFunc;
        }

        public RemoveItemTransform<T> WithMetadata(ItemMetadataValue<T> metadata)
        {
            _metadata.Add(metadata);
            return this;
        }

        public RemoveItemTransform<T> WithMetadata(
            string metadataName,
            string metadataValue,
            bool expressedAsAttribute = false)
        {
            _metadata.Add(new ItemMetadataValue<T>(
                metadataName,
                metadataValue,
                expressedAsAttribute: expressedAsAttribute));
            return this;
        }

        public override ProjectItemElement ConditionallyTransform(T source)
        {
            string removeValue = _removeValueFunc(source);
            
            var item = _itemObjectGenerator.AddItem(_itemName, "PlaceHolderSinceNullOrEmptyCannotBePassedToConstructor");
            item.Include = null;
            item.Remove = removeValue;

            foreach (var metadata in _metadata)
            {
                if (metadata.ShouldWriteMetadata(source))
                {
                    var metametadata = item.AddMetadata(metadata.MetadataName, metadata.GetMetadataValue(source));
                    metametadata.Condition = metadata.Condition;
                    metametadata.ExpressedAsAttribute = metadata.ExpressedAsAttribute;
                }
            }

            return item;
        }
    }
}
