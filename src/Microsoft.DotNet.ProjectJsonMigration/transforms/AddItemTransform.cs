// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Microsoft.DotNet.ProjectJsonMigration.Models;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    public class AddItemTransform<T> : ConditionalTransform<T, ProjectItemElement>
    {
        private readonly ProjectRootElement _itemObjectGenerator = ProjectRootElement.Create();

        private readonly string _itemName;
        private readonly string _includeValue;
        private readonly string _excludeValue;

        private readonly Func<T, string> _includeValueFunc;
        private readonly Func<T, string> _excludeValueFunc;

        private readonly List<ItemMetadataValue<T>> _metadata = new List<ItemMetadataValue<T>>();

        public AddItemTransform(
            string itemName,
            IEnumerable<string> includeValues,
            IEnumerable<string> excludeValues,
            Func<T, bool> condition)
            : this(itemName, string.Join(";", includeValues), string.Join(";", excludeValues), condition) { }

        public AddItemTransform(
            string itemName,
            Func<T, string> includeValueFunc,
            Func<T, string> excludeValueFunc,
            Func<T, bool> condition)
            : base(condition)
        {
            _itemName = itemName;
            _includeValueFunc = includeValueFunc;
            _excludeValueFunc = excludeValueFunc;
        }

        public AddItemTransform(
            string itemName,
            string includeValue,
            Func<T, string> excludeValueFunc,
            Func<T, bool> condition)
            : base(condition)
        {
            _itemName = itemName;
            _includeValue = includeValue;
            _excludeValueFunc = excludeValueFunc;
        }

        public AddItemTransform(
            string itemName,
            Func<T, string> includeValueFunc,
            string excludeValue,
            Func<T, bool> condition)
            : base(condition)
        {
            _itemName = itemName;
            _includeValueFunc = includeValueFunc;
            _excludeValue = excludeValue;
        }

        public AddItemTransform(
            string itemName,
            string includeValue,
            string excludeValue,
            Func<T, bool> condition)
            : base(condition)
        {
            _itemName = itemName;
            _includeValue = includeValue;
            _excludeValue = excludeValue;
        }

        public AddItemTransform<T> WithMetadata(string metadataName, string metadataValue)
        {
            _metadata.Add(new ItemMetadataValue<T>(metadataName, metadataValue));
            return this;
        }

        public AddItemTransform<T> WithMetadata(string metadataName, Func<T, string> metadataValueFunc)
        {
            _metadata.Add(new ItemMetadataValue<T>(metadataName, metadataValueFunc));
            return this;
        }

        public AddItemTransform<T> WithMetadata(ItemMetadataValue<T> metadata)
        {
            _metadata.Add(metadata);
            return this;
        }

        public override ProjectItemElement ConditionallyTransform(T source)
        {
            string includeValue = _includeValue ?? _includeValueFunc(source);
            string excludeValue = _excludeValue ?? _excludeValueFunc(source);

            var item = _itemObjectGenerator.AddItem(_itemName, includeValue);
            item.Exclude = excludeValue;

            foreach (var metadata in _metadata)
            {
                item.AddMetadata(metadata.MetadataName, metadata.GetMetadataValue(source));
            }

            return item;
        }
    }
}
