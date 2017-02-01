// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Internal.ProjectModel.Files;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectJsonMigration.Models;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    internal class RemoveContextTransform : ConditionalTransform<IncludeContext, IEnumerable<ProjectItemElement>>
    {
        protected virtual Func<string, RemoveItemTransform<IncludeContext>> RemoveTransformGetter =>
            (itemName) => new RemoveItemTransform<IncludeContext>(
                itemName,
                includeContext =>
                {
                    var fullRemoveSet = includeContext.ExcludePatterns.OrEmptyIfNull()
                                        .Union(includeContext.ExcludeFiles.OrEmptyIfNull());

                    return FormatGlobPatternsForMsbuild(fullRemoveSet, includeContext.SourceBasePath);
                },
                includeContext => 
                {
                    return includeContext != null &&
                        ( 
                            (includeContext.ExcludePatterns != null && includeContext.ExcludePatterns.Count > 0)
                            ||
                            (includeContext.ExcludeFiles != null && includeContext.ExcludeFiles.Count > 0)
                        );
                });

        private Func<string, string, RemoveItemTransform<IncludeContext>> MappingsRemoveTransformGetter =>
            (itemName, targetPath) => AddMappingToTransform(RemoveTransformGetter(itemName), targetPath);

        private Func<RemoveItemTransform<IncludeContext>, string, RemoveItemTransform<IncludeContext>> _mappingsToTransfrom;

        private readonly string _itemName;
        private readonly List<ItemMetadataValue<IncludeContext>> _metadata = new List<ItemMetadataValue<IncludeContext>>();

        public RemoveContextTransform(
            string itemName,
            Func<IncludeContext, bool> condition = null
            ) : base(condition)
        {
            _itemName = itemName;

            _mappingsToTransfrom = (removeItemTransform, targetPath) =>
            {
                var msbuildLinkMetadataValue = ConvertTargetPathToMsbuildMetadata(targetPath);

                return removeItemTransform.WithMetadata("Link", msbuildLinkMetadataValue);
            };
        }

        public override IEnumerable<ProjectItemElement> ConditionallyTransform(IncludeContext source)
        {
            var transformSet = CreateTransformSet(source);
            return transformSet.Select(t => t.Item1.Transform(t.Item2));
        }

        private IEnumerable<Tuple<RemoveItemTransform<IncludeContext>, IncludeContext>> CreateTransformSet(IncludeContext source)
        {
            var transformSet = new List<Tuple<RemoveItemTransform<IncludeContext>, IncludeContext>>
            {
                Tuple.Create(RemoveTransformGetter(_itemName), source)
            };

            if (source == null)
            {
                return transformSet;
            }

            // Mappings must be executed before the transform set to prevent a the
            // non-mapped items that will merge with mapped items from being encompassed
            foreach (var mappingEntry in source.Mappings.OrEmptyIfNull())
            {
                var targetPath = mappingEntry.Key;
                var includeContext = mappingEntry.Value;

                transformSet.Insert(0,
                    Tuple.Create(
                        MappingsRemoveTransformGetter(_itemName, targetPath),
                        includeContext));
            }

            foreach (var metadataElement in _metadata)
            {
                foreach (var transform in transformSet)
                {
                    transform.Item1.WithMetadata(metadataElement);
                }
            }

            return transformSet;
        }

        private RemoveItemTransform<IncludeContext> AddMappingToTransform(
            RemoveItemTransform<IncludeContext> removeItemTransform,
            string targetPath)
        {
            return _mappingsToTransfrom(removeItemTransform, targetPath);
        }
    }
}
