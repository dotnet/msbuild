// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed class IncludeOperation : ItemOperation
    {
        private readonly int _includeId;
        private readonly string? _rootDirectory;
        private readonly ImmutableSegmentedList<string> _excludes;
        private readonly ImmutableArray<ProjectMetadataElement> _metadata;
        private bool? _needToExpandMetadataForEachItem;

        public IncludeOperation(
            LazyItemEvaluator<P, I, M, D> lazyEvaluator,
            ProjectItemElement itemElement,
            ItemSpec<P, I> itemSpec,
            bool conditionResult,
            ItemListBookmarks itemListBookmarks,
            int includeId,
            string? rootDirectory,
            ImmutableSegmentedList<string> excludes,
            ImmutableArray<ProjectMetadataElement> metadata)
            : base(lazyEvaluator, itemElement, itemSpec, conditionResult, itemListBookmarks)
        {
            _includeId = includeId;
            _rootDirectory = rootDirectory;
            _excludes = excludes;
            _metadata = metadata;
        }

        protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            var items = SelectItems(laterRemoveGlobPatterns);
            MutateItems(items);
            SaveItems(items, listBuilder);
        }

        /// <summary>
        ///  Creates the items contributed by this Include operation.
        /// </summary>
        [SuppressMessage("Microsoft.Dispose", "CA2000:Dispose objects before losing scope", Justification = "_lazyEvaluator._evaluationProfiler has own dipose logic.")]
        private ImmutableArray<I> SelectItems(ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            using RefArrayBuilder<I> itemsToAdd = default;
            using RefArrayBuilder<string> excludePatterns = default;

            if (!_excludes.IsEmpty)
            {
                // STEP 4: Evaluate, split, expand and subtract any Exclude
                foreach (string exclude in _excludes)
                {
                    string expandedExclude = _expander.ExpandIntoStringLeaveEscaped(
                        exclude,
                        ExpanderOptions.ExpandPropertiesAndItems,
                        _itemElement.ExcludeLocation);

                    foreach (string value in ExpressionShredder.SplitSemiColonSeparatedList(expandedExclude))
                    {
                        excludePatterns.Add(value);
                    }
                }
            }

            ImmutableHashSet<string>? excludePatternsForGlobs = null;
            FileSpecMatcherTester?[]? matchers = null;

            foreach (var fragment in _itemSpec.Fragments)
            {
                if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemVectorFragment)
                {
                    // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                    var itemsFromExpression = _expander.ExpandExpressionCaptureIntoItems(
                        itemVectorFragment.Capture,
                        _itemListBookmarks,
                        _itemFactory,
                        ExpanderOptions.ExpandItems,
                        includeNullEntries: false,
                        isTransformExpression: out _,
                        elementLocation: _itemElement.IncludeLocation);

                    if (excludePatterns.Count > 0)
                    {
                        matchers ??= new FileSpecMatcherTester?[excludePatterns.Count];

                        foreach (var item in itemsFromExpression)
                        {
                            if (!ExcludeTester(_rootDirectory, excludePatterns.AsSpan(), matchers, item.EvaluatedInclude))
                            {
                                itemsToAdd.Add(item);
                            }
                        }
                    }
                    else
                    {
                        foreach (I item in itemsFromExpression)
                        {
                            itemsToAdd.Add(item);
                        }
                    }
                }
                else if (fragment is ValueFragment valueFragment)
                {
                    string value = valueFragment.TextFragment;
                    if (excludePatterns.Count > 0)
                    {
                        matchers ??= new FileSpecMatcherTester?[excludePatterns.Count];
                        if (ExcludeTester(_rootDirectory, excludePatterns.AsSpan(), matchers, EscapingUtilities.UnescapeAll(value)))
                        {
                            continue;
                        }
                    }

                    itemsToAdd.Add(_itemFactory.CreateItem(value, value, _itemElement.ContainingProject.FullPath));
                }
                else if (fragment is GlobFragment globFragment)
                {
                    // If this item is behind a false condition and represents a full drive/filesystem scan, expanding it is
                    // almost certainly undesired. It should be skipped to avoid evaluation taking an excessive amount of time.
                    bool skipGlob = !_conditionResult && globFragment.IsFullFileSystemScan && !Traits.Instance.EscapeHatches.AlwaysEvaluateDangerousGlobs;
                    if (!skipGlob)
                    {
                        string glob = globFragment.TextFragment;

                        excludePatternsForGlobs ??= BuildExcludePatternsForGlobs(laterRemoveGlobPatterns, excludePatterns.AsSpan());

                        string[] includeSplitFilesEscaped;
                        if (MSBuildEventSource.Log.IsEnabled())
                        {
                            MSBuildEventSource.Log.ExpandGlobStart(_rootDirectory ?? string.Empty, glob, string.Join(", ", excludePatternsForGlobs));
                        }

                        using (_lazyEvaluator._evaluationProfiler.TrackGlob(_rootDirectory, glob, excludePatternsForGlobs))
                        {
                            includeSplitFilesEscaped = EngineFileUtilities.GetFileListEscaped(
                                _rootDirectory,
                                glob,
                                excludePatternsForGlobs,
                                fileMatcher: FileMatcher,
                                loggingMechanism: _lazyEvaluator._loggingContext,
                                includeLocation: _itemElement.IncludeLocation,
                                excludeLocation: _itemElement.ExcludeLocation);
                        }

                        if (MSBuildEventSource.Log.IsEnabled())
                        {
                            MSBuildEventSource.Log.ExpandGlobStop(_rootDirectory ?? string.Empty, glob, string.Join(", ", excludePatternsForGlobs));
                        }

                        foreach (string includeSplitFileEscaped in includeSplitFilesEscaped)
                        {
                            itemsToAdd.Add(_itemFactory.CreateItem(includeSplitFileEscaped, glob, _itemElement.ContainingProject.FullPath));
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException(fragment.GetType().ToString());
                }
            }

            // ToImmutable copies the selected items before the temporary pooled buffer is returned.
            return itemsToAdd.ToImmutable();

            static bool ExcludeTester(string? directory, ReadOnlySpan<string> excludePatterns, FileSpecMatcherTester?[] matchers, string item)
            {
                Debug.Assert(excludePatterns.Length > 0, $"{nameof(ExcludeTester)} must be called with exclude patterns.");

                bool found = false;
                for (int i = 0; i < matchers.Length; ++i)
                {
                    FileSpecMatcherTester? matcher = matchers[i];
                    if (!matcher.HasValue)
                    {
                        matcher = FileSpecMatcherTester.Parse(directory, excludePatterns[i]);
                        matchers[i] = matcher;
                    }

                    if (matcher.Value.IsMatch(item))
                    {
                        found = true;
                        break;
                    }
                }

                return found;
            }
        }

        private static ImmutableHashSet<string> BuildExcludePatternsForGlobs(ImmutableHashSet<string> laterRemoveGlobPatterns, ReadOnlySpan<string> excludePatterns)
        {
            if (excludePatterns.Length > 0)
            {
                var builder = ImmutableHashSet.CreateBuilder<string>(FileUtilities.PathComparer);

                foreach (string exclude in excludePatterns)
                {
                    builder.Add(exclude);
                }

                if (laterRemoveGlobPatterns.Count > 0)
                {
                    builder.UnionWith(laterRemoveGlobPatterns);
                }

                return builder.ToImmutable();
            }

            return laterRemoveGlobPatterns;
        }

        private void MutateItems(ImmutableArray<I> items)
        {
            if (_metadata.IsEmpty)
            {
                return;
            }

            _needToExpandMetadataForEachItem ??= NeedToExpandMetadataForEachItem(_metadata, out _);

            // Constant metadata must still be evaluated when selection is empty, preserving diagnostics.
            DecorateItemsWithMetadata(items.Select(i => new ItemBatchingContext(i)), _metadata, _needToExpandMetadataForEachItem);
        }

        private void SaveItems(ImmutableArray<I> items, OrderedItemDataCollection.Builder listBuilder)
        {
            foreach (var item in items)
            {
                listBuilder.Add(new ItemData(item, _includeId));
            }
        }
    }
}
