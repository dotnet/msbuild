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
    internal class IncludeContextTransform : ConditionalTransform<IncludeContext, IEnumerable<ProjectItemElement>>
    {
        protected virtual Func<string, AddItemTransform<IncludeContext>> IncludeFilesExcludeFilesTransformGetter =>
            (itemName) =>
                new AddItemTransform<IncludeContext>(
                    itemName,
                    includeContext => FormatGlobPatternsForMsbuild(includeContext.IncludeFiles.OrEmptyIfNull()
                                                                       .Where((pattern) => !_excludePatternRule(pattern)),
                                                                   includeContext.SourceBasePath),
                    includeContext => FormatGlobPatternsForMsbuild(includeContext.ExcludeFiles, includeContext.SourceBasePath),
                    includeContext => includeContext != null 
                        && includeContext.IncludeFiles != null 
                        && includeContext.IncludeFiles.Where((pattern) => !_excludePatternRule(pattern)).Count() > 0);

        protected virtual Func<string, AddItemTransform<IncludeContext>> IncludeExcludeTransformGetter =>
            (itemName) => new AddItemTransform<IncludeContext>(
                itemName,
                includeContext => 
                {
                    var fullIncludeSet = includeContext.IncludePatterns.OrEmptyIfNull();
                    if (_emitBuiltInIncludes)
                    {
                        fullIncludeSet = fullIncludeSet.Union(includeContext.BuiltInsInclude.OrEmptyIfNull());
                    }

                    fullIncludeSet = fullIncludeSet.Where((pattern) => !_excludePatternRule(pattern));

                    return FormatGlobPatternsForMsbuild(fullIncludeSet, includeContext.SourceBasePath);
                },
                includeContext =>
                {
                    var fullExcludeSet = includeContext.ExcludePatterns.OrEmptyIfNull()
                                         .Union(includeContext.BuiltInsExclude.OrEmptyIfNull())
                                         .Union(includeContext.ExcludeFiles.OrEmptyIfNull());

                    return FormatGlobPatternsForMsbuild(fullExcludeSet, includeContext.SourceBasePath);
                },
                includeContext => 
                {
                    return includeContext != null &&
                        ( 
                            (includeContext.IncludePatterns != null && includeContext.IncludePatterns.Where((pattern) => !_excludePatternRule(pattern)).Count() > 0)
                            ||
                            (_emitBuiltInIncludes && 
                             includeContext.BuiltInsInclude != null && 
                             includeContext.BuiltInsInclude.Count > 0)
                        );
                });

        private Func<string, string, AddItemTransform<IncludeContext>> MappingsIncludeFilesExcludeFilesTransformGetter =>
            (itemName, targetPath) => AddMappingToTransform(IncludeFilesExcludeFilesTransformGetter(itemName), targetPath);

        private Func<string, string, AddItemTransform<IncludeContext>> MappingsIncludeExcludeTransformGetter =>
            (itemName, targetPath) => AddMappingToTransform(IncludeExcludeTransformGetter(itemName), targetPath);

        private Func<AddItemTransform<IncludeContext>, string, AddItemTransform<IncludeContext>> _mappingsToTransfrom;

        private readonly string _itemName;
        private bool _transformMappings;
        private Func<string, bool> _excludePatternRule;
        private bool _emitBuiltInIncludes;
        private readonly List<ItemMetadataValue<IncludeContext>> _metadata = new List<ItemMetadataValue<IncludeContext>>();

        public IncludeContextTransform(
            string itemName,
            bool transformMappings = true,
            Func<IncludeContext, bool> condition = null,
            bool emitBuiltInIncludes = true,
            Func<string, bool> excludePatternsRule = null) : base(condition)
        {
            _itemName = itemName;
            _transformMappings = transformMappings;
            _emitBuiltInIncludes = emitBuiltInIncludes;
            _excludePatternRule = excludePatternsRule ?? ((pattern) => false);

            _mappingsToTransfrom = (addItemTransform, targetPath) =>
            {
                var msbuildLinkMetadataValue = ConvertTargetPathToMsbuildMetadata(targetPath);

                return addItemTransform.WithMetadata("Link", msbuildLinkMetadataValue);
            };
        }

        public IncludeContextTransform WithMetadata(
            string metadataName,
            string metadataValue,
            string condition = null)
        {
            _metadata.Add(new ItemMetadataValue<IncludeContext>(metadataName, metadataValue, condition));
            return this;
        }

        public IncludeContextTransform WithMetadata(
            string metadataName,
            Func<IncludeContext, string> metadataValueFunc,
            string condition = null)
        {
            _metadata.Add(new ItemMetadataValue<IncludeContext>(
                metadataName,
                metadataValueFunc,
                condition: condition));
            return this;
        }

        public IncludeContextTransform WithMappingsToTransform(
            Func<AddItemTransform<IncludeContext>, string, AddItemTransform<IncludeContext>> mappingsToTransfrom)
        {
            _mappingsToTransfrom = mappingsToTransfrom;
            return this;
        }

        private IEnumerable<Tuple<AddItemTransform<IncludeContext>, IncludeContext>> CreateTransformSet(IncludeContext source)
        {
            var transformSet = new List<Tuple<AddItemTransform<IncludeContext>, IncludeContext>>
            {
                Tuple.Create(IncludeFilesExcludeFilesTransformGetter(_itemName), source),
                Tuple.Create(IncludeExcludeTransformGetter(_itemName), source)
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
                        MappingsIncludeExcludeTransformGetter(_itemName, targetPath),
                        includeContext));

                transformSet.Insert(0,
                    Tuple.Create(
                        MappingsIncludeFilesExcludeFilesTransformGetter(_itemName, targetPath), 
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

        public override IEnumerable<ProjectItemElement> ConditionallyTransform(IncludeContext source)
        {
            var transformSet = CreateTransformSet(source);
            return transformSet.Select(t => t.Item1.Transform(t.Item2));
        }

        private AddItemTransform<IncludeContext> AddMappingToTransform(
            AddItemTransform<IncludeContext> addItemTransform, 
            string targetPath)
        {
            return _mappingsToTransfrom(addItemTransform, targetPath);
        }
    }
}
