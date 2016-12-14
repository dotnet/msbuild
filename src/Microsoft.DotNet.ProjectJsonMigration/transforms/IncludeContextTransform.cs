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
                    includeContext => FormatGlobPatternsForMsbuild(includeContext.IncludeFiles, includeContext.SourceBasePath),
                    includeContext => FormatGlobPatternsForMsbuild(includeContext.ExcludeFiles, includeContext.SourceBasePath),
                    includeContext => includeContext != null 
                        && includeContext.IncludeFiles != null 
                        && includeContext.IncludeFiles.Count > 0);

        protected virtual Func<string, AddItemTransform<IncludeContext>> IncludeExcludeTransformGetter =>
            (itemName) => new AddItemTransform<IncludeContext>(
                itemName,
                includeContext => 
                {
                    var fullIncludeSet = includeContext.IncludePatterns.OrEmptyIfNull()
                                         .Union(includeContext.BuiltInsInclude.OrEmptyIfNull());

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
                            (includeContext.IncludePatterns != null && includeContext.IncludePatterns.Count > 0)
                            ||
                            (includeContext.BuiltInsInclude != null && includeContext.BuiltInsInclude.Count > 0)
                        );
                });

        private Func<string, string, AddItemTransform<IncludeContext>> MappingsIncludeFilesExcludeFilesTransformGetter =>
            (itemName, targetPath) => AddMappingToTransform(IncludeFilesExcludeFilesTransformGetter(itemName), targetPath);

        private Func<string, string, AddItemTransform<IncludeContext>> MappingsIncludeExcludeTransformGetter =>
            (itemName, targetPath) => AddMappingToTransform(IncludeExcludeTransformGetter(itemName), targetPath);

        private Func<AddItemTransform<IncludeContext>, string, AddItemTransform<IncludeContext>> _mappingsToTransfrom;

        private readonly string _itemName;
        private bool _transformMappings;
        private readonly List<ItemMetadataValue<IncludeContext>> _metadata = new List<ItemMetadataValue<IncludeContext>>();

        public IncludeContextTransform(
            string itemName,
            bool transformMappings = true,
            Func<IncludeContext, bool> condition = null) : base(condition)
        {
            _itemName = itemName;
            _transformMappings = transformMappings;

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

        protected string FormatGlobPatternsForMsbuild(IEnumerable<string> patterns, string projectDirectory)
        {
            if (patterns == null)
            {
                return string.Empty;
            }

            List<string> mutatedPatterns = new List<string>(patterns.Count());

            foreach (var pattern in patterns)
            {
                // Do not use forward slashes
                // https://github.com/Microsoft/msbuild/issues/724
                var mutatedPattern = pattern.Replace('/', '\\');

                // MSBuild cannot copy directories
                mutatedPattern = ReplaceDirectoriesWithGlobs(mutatedPattern, projectDirectory);

                mutatedPatterns.Add(mutatedPattern);
            }

            return string.Join(";", mutatedPatterns);
        }

        private string ReplaceDirectoriesWithGlobs(string pattern, string projectDirectory)
        {
            if (PatternIsDirectory(pattern, projectDirectory))
            {
                return $"{pattern.TrimEnd(new char[] { '\\' })}\\**\\*";
            }
            else
            {
                return pattern;
            }
        }

        private AddItemTransform<IncludeContext> AddMappingToTransform(
            AddItemTransform<IncludeContext> addItemTransform, 
            string targetPath)
        {
            return _mappingsToTransfrom(addItemTransform, targetPath);
        }

        private bool PatternIsDirectory(string pattern, string projectDirectory)
        {
            // TODO: what about /some/path/**/somedir?
            // Should this even be migrated?
            var path = pattern;

            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(projectDirectory, path);
            }

            return Directory.Exists(path);
        }

        private string ConvertTargetPathToMsbuildMetadata(string targetPath)
        {
            var targetIsFile = MappingsTargetPathIsFile(targetPath);

            if (targetIsFile)
            {
                return targetPath;
            }

            return $"{targetPath}%(FileName)%(Extension)";
        }

        private bool MappingsTargetPathIsFile(string targetPath)
        {
            var normalizedTargetPath = PathUtility.GetPathWithDirectorySeparator(targetPath);

            return normalizedTargetPath[normalizedTargetPath.Length - 1] != Path.DirectorySeparatorChar;
        }
    }
}
