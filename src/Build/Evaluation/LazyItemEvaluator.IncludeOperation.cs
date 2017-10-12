// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        internal class IncludeOperation : LazyItemOperation
        {
            readonly int _elementOrder;
            
            readonly string _rootDirectory;

            readonly ImmutableList<string> _excludes;

            readonly ImmutableList<ProjectMetadataElement> _metadata;

            public IncludeOperation(IncludeOperationBuilder builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(builder, lazyEvaluator)
            {
                _elementOrder = builder.ElementOrder;
                _rootDirectory = builder.RootDirectory;

                _excludes = builder.Excludes.ToImmutable();
                _metadata = builder.Metadata.ToImmutable();
            }

            protected override ImmutableList<I> SelectItems(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                return ComputeItemsFromElement(
                    _itemSpec,
                    _excludes,
                    _itemElement.IncludeLocation,
                    _itemElement.ExcludeLocation,
                    globsToIgnore,
                    _rootDirectory,
                    _itemElement.ContainingProject.FullPath,
                    _expander,
                    _evaluatorData,
                    _itemFactory,
                    EntriesCache);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ImmutableList<I> ComputeItemsFromElement(
                ItemSpec<P, I> itemSpec,
                ImmutableList<string> excludes,
                IElementLocation includeLocation,
                IElementLocation excludeLocation,
                ImmutableHashSet<string> globsToIgnore,
                string rootDirectory, string projectPath,
                Expander<P, I> expander,
                IItemProvider<I> itemProvider,
                IItemFactory<I, I> itemFactory,
                ConcurrentDictionary<string, ImmutableArray<string>> entriesCache)
            {
                var itemsToAdd = ImmutableList.CreateBuilder<I>();

                Lazy<Func<string, bool>> excludeTester = null;
                ImmutableList<string>.Builder excludePatterns = ImmutableList.CreateBuilder<string>();
                if (excludes != null)
                {
                    // STEP 4: Evaluate, split, expand and subtract any Exclude
                    foreach (string exclude in excludes)
                    {
                        string excludeExpanded = expander.ExpandIntoStringLeaveEscaped(exclude,
                            ExpanderOptions.ExpandPropertiesAndItems, excludeLocation);
                        var excludeSplits = ExpressionShredder.SplitSemiColonSeparatedList(excludeExpanded);
                        excludePatterns.AddRange(excludeSplits);
                    }

                    if (excludePatterns.Count > 0)
                    {
                        excludeTester = new Lazy<Func<string, bool>>(() =>
                            EngineFileUtilities.GetFileSpecMatchTester(excludePatterns, rootDirectory));
                    }
                }

                ISet<string> excludePatternsForGlobs = null;

                foreach (var fragment in itemSpec.Fragments)
                {
                    if (fragment is ItemExpressionFragment<P, I>)
                    {
                        // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                        bool throwaway;
                        var itemsFromExpression = expander.ExpandExpressionCaptureIntoItems(
                            ((ItemExpressionFragment<P, I>) fragment).Capture, itemProvider, itemFactory,
                            ExpanderOptions.ExpandItems,
                            false /* do not include null expansion results */, out throwaway, includeLocation);

                        if (excludeTester != null)
                        {
                            itemsToAdd.AddRange(itemsFromExpression.Where(item => !excludeTester.Value(item.EvaluatedInclude)));
                        }
                        else
                        {
                            itemsToAdd.AddRange(itemsFromExpression);
                        }
                    }
                    else if (fragment is ValueFragment)
                    {
                        string value = ((ValueFragment) fragment).ItemSpecFragment;

                        if (excludeTester == null ||
                            !excludeTester.Value(EscapingUtilities.UnescapeAll(value)))
                        {
                            var item = itemFactory.CreateItem(value, value, projectPath);
                            itemsToAdd.Add(item);
                        }
                    }
                    else if (fragment is GlobFragment)
                    {
                        string glob = ((GlobFragment) fragment).ItemSpecFragment;

                        if (excludePatternsForGlobs == null)
                        {
                            excludePatternsForGlobs = BuildExcludePatternsForGlobs(globsToIgnore, excludePatterns);
                        }

                        string[] includeSplitFilesEscaped = EngineFileUtilities.GetFileListEscaped(
                            rootDirectory,
                            glob,
                            excludePatternsForGlobs,
                            entriesCache: entriesCache
                        );

                        foreach (string includeSplitFileEscaped in includeSplitFilesEscaped)
                        {
                            itemsToAdd.Add(itemFactory.CreateItem(includeSplitFileEscaped, glob,
                                projectPath));
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(fragment.GetType().ToString());
                    }
                }

                return itemsToAdd.ToImmutable();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ISet<string> BuildExcludePatternsForGlobs(ImmutableHashSet<string> globsToIgnore, ImmutableList<string>.Builder excludePatterns)
            {
                var anyExcludes = excludePatterns.Count > 0;
                var anyGlobstoIgnore = globsToIgnore.Count > 0;

                if (anyGlobstoIgnore && anyExcludes)
                {
                    return excludePatterns.Concat(globsToIgnore).ToImmutableHashSet();
                }

                return anyExcludes ? excludePatterns.ToImmutableHashSet() : globsToIgnore;
            }

            protected override void MutateItems(ImmutableList<I> items)
            {
                DecorateItemsWithMetadata(items, _metadata);
            }

            protected override void SaveItems(ImmutableList<I> items, ImmutableList<ItemData>.Builder listBuilder)
            {
                foreach (var item in items)
                {
                    listBuilder.Add(new ItemData(item, _itemElement, _elementOrder, _conditionResult));
                }
            }
        }

        internal class IncludeOperationBuilder : OperationBuilderWithMetadata
        {
            public int ElementOrder { get; set; }
            public string RootDirectory { get; set; }

            public ImmutableList<string>.Builder Excludes { get; set; } = ImmutableList.CreateBuilder<string>();

            public IncludeOperationBuilder(ProjectItemElement itemElement, bool conditionResult) : base(itemElement, conditionResult)
            {
            }
        }
    }
}
