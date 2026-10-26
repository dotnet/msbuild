// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
#if DEBUG
using System.Diagnostics;
#endif
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract partial class ItemOperation
    {
        private bool _isBookmarked;

        protected readonly LazyItemEvaluator<P, I, M, D> _lazyEvaluator;
        protected readonly ProjectItemElement _itemElement;
        protected readonly ItemListBookmarks _itemListBookmarks;
        protected readonly Expander<P, I> _expander;
        protected readonly bool _conditionResult;

        // This is used only when evaluating an expression, which instantiates
        //  the items and then removes them
        protected readonly IItemFactory<I, I> _itemFactory;

        protected ItemOperation(
            LazyItemEvaluator<P, I, M, D> lazyEvaluator,
            ProjectItemElement itemElement,
            bool conditionResult,
            ItemListBookmarks itemListBookmarks)
        {
            _itemElement = itemElement;
            _conditionResult = conditionResult;
            _lazyEvaluator = lazyEvaluator;
            _itemListBookmarks = itemListBookmarks;
            _itemFactory = new ItemFactoryWrapper(_itemElement, _lazyEvaluator._itemFactory);
            _expander = new Expander<P, I>(
                _lazyEvaluator._data,
                _itemListBookmarks,
                _lazyEvaluator.EvaluationContext,
                _lazyEvaluator._loggingContext);
        }

        protected FileMatcher FileMatcher => _lazyEvaluator.FileMatcher;

        /// <summary>
        ///  Gets whether a bookmark identifies the item state after this operation.
        /// </summary>
        public bool IsBookmarked => _isBookmarked;

        /// <summary>
        ///  Marks this position so snapshots needed by bookmarks remain cached.
        /// </summary>
        public void MarkAsBookmarked() => _isBookmarked = true;

        /// <summary>
        ///  Applies the operation and caches its snapshot when this position has a bookmark.
        /// </summary>
        /// <param name="listBuilder">The items produced by preceding operations.</param>
        /// <param name="laterRemoveGlobPatterns">The later Remove glob patterns used for this computation.</param>
        public void Apply(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
#if DEBUG
            Debug.Assert(!_lazyEvaluator.TryGetCachedItems(this, laterRemoveGlobPatterns, out _),
                "Apply requires an uncached operation and later Remove glob pattern set");
#endif

            MSBuildEventSource.Log.ApplyLazyItemOperationsStart(_itemElement.ItemType);
            using (_lazyEvaluator._evaluationProfiler.TrackElement(_itemElement))
            {
                ApplyImpl(listBuilder, laterRemoveGlobPatterns);
            }
            MSBuildEventSource.Log.ApplyLazyItemOperationsStop(_itemElement.ItemType);

            // Snapshot creation remains outside the operation's profiling and event scope.
            CacheResultIfBookmarked(listBuilder, laterRemoveGlobPatterns);
        }

        /// <summary>
        ///  Caches the completed snapshot when a bookmark identifies this position.
        /// </summary>
        /// <param name="listBuilder">The computed items through this operation, under the supplied later Remove glob patterns.</param>
        /// <param name="laterRemoveGlobPatterns">The later Remove glob patterns used for this computation.</param>
        public void CacheResultIfBookmarked(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            if (_isBookmarked)
            {
                _lazyEvaluator.CacheItems(this, laterRemoveGlobPatterns, listBuilder.ToImmutable());
            }

#if DEBUG
            Debug.Assert(_lazyEvaluator.TryGetCachedItems(this, laterRemoveGlobPatterns, out _) == _isBookmarked,
                "Only captured intermediate operation results should be cached");
#endif
        }

        protected abstract void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns);

        protected void DecorateItemsWithMetadata(IEnumerable<ItemBatchingContext> itemBatchingContexts, ImmutableArray<ProjectMetadataElement> metadata, bool? needToExpandMetadata = null)
        {
            if (metadata.Length > 0)
            {
                ////////////////////////////////////////////////////
                // UNDONE: Implement batching here.
                //
                // We want to allow built-in metadata in metadata values here.
                // For example, so that an Idl file can specify that its Tlb output should be named %(Filename).tlb.
                //
                // In other words, we want batching. However, we won't need to go to the trouble of using the regular batching code!
                // That's because that code is all about grouping into buckets of similar items. In this context, we're not
                // invoking a task, and it's fine to process each item individually, which will always give the correct results.
                //
                // For the CTP, to make the minimal change, we will not do this quite correctly.
                //
                // We will do this:
                // -- check whether any metadata values or their conditions contain any bare built-in metadata expressions,
                //    or whether they contain any custom metadata && the Include involved an @(itemlist) expression.
                // -- if either case is found, we go ahead and evaluate all the metadata separately for each item.
                // -- otherwise we can do the old thing (evaluating all metadata once then applying to all items)
                //
                // This algorithm gives the correct results except when:
                // -- batchable expressions exist on the include, exclude, or condition on the item element itself
                //
                // It means that 99% of cases still go through the old code, which is best for the CTP.
                // When we ultimately implement this correctly, we should make sure we optimize for the case of very many items
                // and little metadata, none of which varies between items.

                // Do not expand properties as they have been already expanded by the lazy evaluator upon item operation construction.
                // Prior to lazy evaluation ExpanderOptions.ExpandAll was used.
                const ExpanderOptions metadataExpansionOptions = ExpanderOptions.ExpandAll;

                needToExpandMetadata ??= NeedToExpandMetadataForEachItem(metadata, out _);

                if (needToExpandMetadata.Value)
                {
                    foreach (var itemContext in itemBatchingContexts)
                    {
                        _expander.Metadata = itemContext.GetMetadataTable();

                        foreach (var metadataElement in metadata)
                        {
                            if (!_lazyEvaluator.EvaluateCondition(metadataElement, _expander, metadataExpansionOptions, ParserOptions.AllowAll))
                            {
                                continue;
                            }

                            string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadataElement.Value, metadataExpansionOptions, metadataElement.Location);

                            itemContext.OperationItem.SetMetadata(metadataElement, FileUtilities.MaybeAdjustFilePath(evaluatedValue, metadataElement.ContainingProject.DirectoryPath));
                        }
                    }

                    // End of legal area for metadata expressions.
                    _expander.Metadata = null;
                }
                // End of pseudo batching
                ////////////////////////////////////////////////////
                // Start of old code
                else
                {
                    // Metadata expressions are allowed here.
                    // Temporarily gather and expand these in a table so they can reference other metadata elements above.
                    EvaluatorMetadataTable metadataTable = new EvaluatorMetadataTable(_itemElement.ItemType, capacity: metadata.Length);
                    _expander.Metadata = metadataTable;

                    // Also keep a list of everything so we can get the predecessor objects correct.
                    List<KeyValuePair<ProjectMetadataElement, string>> metadataList = new(metadata.Length);

                    foreach (var metadataElement in metadata)
                    {
                        // Because of the checking above, it should be safe to expand metadata in conditions; the condition
                        // will be true for either all the items or none
                        if (!_lazyEvaluator.EvaluateCondition(metadataElement, _expander, metadataExpansionOptions, ParserOptions.AllowAll))
                        {
                            continue;
                        }

                        string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadataElement.Value, metadataExpansionOptions, metadataElement.Location);
                        evaluatedValue = FileUtilities.MaybeAdjustFilePath(evaluatedValue, metadataElement.ContainingProject.DirectoryPath);

                        metadataTable.SetValue(metadataElement, evaluatedValue);
                        metadataList.Add(new KeyValuePair<ProjectMetadataElement, string>(metadataElement, evaluatedValue));
                    }

                    // Apply those metadata to each item
                    // Note that several items could share the same metadata objects

                    // Set all the items at once to make a potential copy-on-write optimization possible.
                    // This is valuable in the case where one item element evaluates to
                    // many items (either by semicolon or wildcards)
                    // and that item also has the same piece/s of metadata for each item.
                    _itemFactory.SetMetadata(metadataList, itemBatchingContexts.Select(i => i.OperationItem));

                    // End of legal area for metadata expressions.
                    _expander.Metadata = null;
                }
            }
        }

        protected bool NeedToExpandMetadataForEachItem(ImmutableArray<ProjectMetadataElement> metadata, out ItemsAndMetadataPair itemsAndMetadataFound)
        {
            itemsAndMetadataFound = new ItemsAndMetadataPair(null, null);

            foreach (var metadataElement in metadata)
            {
                string expression = metadataElement.Value;
                ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);

                expression = metadataElement.Condition;
                ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);
            }

            bool needToExpandMetadataForEachItem = false;

            if (itemsAndMetadataFound.Metadata?.Values.Count > 0)
            {
                // If there is any metadata present, we need to expand items individually.
                // This ensures correct results for:
                // - Built-in metadata expressions (like %(FileName)) which vary between items
                // - Custom metadata when item list references are involved
                needToExpandMetadataForEachItem = true;
            }

            return needToExpandMetadataForEachItem;
        }
    }
}
