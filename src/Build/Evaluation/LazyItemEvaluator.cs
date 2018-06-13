// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem<M>, IMetadataTable
        where M : class, IMetadatum
        where D : class, IItemDefinition<M>
    {
        private readonly IEvaluatorData<P, I, M, D> _outerEvaluatorData;
        private readonly Expander<P, I> _outerExpander;
        private readonly IEvaluatorData<P, I, M, D> _evaluatorData;
        private readonly Expander<P, I> _expander;
        private readonly IItemFactory<I, I> _itemFactory;
        private readonly LoggingContext _loggingContext;
        private readonly EvaluationProfiler _evaluationProfiler;

        private int _nextElementOrder = 0;


        /// <summary>
        /// The CultureInfo from the invariant culture. Used to avoid allocations for
        /// perfoming IndexOf etc.
        /// </summary>
        private static CompareInfo s_invariantCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

        private Dictionary<string, LazyItemList> _itemLists = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
            new Dictionary<string, LazyItemList>() :
            new Dictionary<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

        protected EngineFileUtilities EngineFileUtilities { get; }

        public LazyItemEvaluator(IEvaluatorData<P, I, M, D> data, IItemFactory<I, I> itemFactory, LoggingContext loggingContext, EvaluationProfiler evaluationProfiler, EvaluationContext evaluationContext)
        {
            _outerEvaluatorData = data;
            _outerExpander = new Expander<P, I>(_outerEvaluatorData, _outerEvaluatorData);
            _evaluatorData = new EvaluatorData(_outerEvaluatorData, itemType => GetItems(itemType));
            _expander = new Expander<P, I>(_evaluatorData, _evaluatorData);
            _itemFactory = itemFactory;
            _loggingContext = loggingContext;
            _evaluationProfiler = evaluationProfiler;

            EngineFileUtilities = new EngineFileUtilities(new FileMatcher(evaluationContext.FileSystem, evaluationContext.FileEntryExpansionCache));
        }

        private ImmutableList<I> GetItems(string itemType)
        {
            LazyItemList itemList = GetItemList(itemType);
            if (itemList == null)
            {
                return ImmutableList<I>.Empty;
            }

            return itemList.GetMatchedItems(ImmutableHashSet<string>.Empty);
        }

        public bool EvaluateConditionWithCurrentState(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        {
            return EvaluateCondition(element, expanderOptions, parserOptions, _expander, this);
        }

        private static bool EvaluateCondition(string condition, ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions, Expander<P, I> expander, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            if (condition?.Length == 0)
            {
                return true;
            }

            using (lazyEvaluator._evaluationProfiler.TrackCondition(element.ConditionLocation, condition))
            {
                bool result = ConditionEvaluator.EvaluateCondition
                    (
                    condition,
                    parserOptions,
                    expander,
                    expanderOptions,
                    GetCurrentDirectoryForConditionEvaluation(element, lazyEvaluator),
                    element.ConditionLocation,
                    lazyEvaluator._loggingContext.LoggingService,
                    lazyEvaluator._loggingContext.BuildEventContext
                    );

                return result;
            }
        }

        private static bool EvaluateCondition(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions, Expander<P, I> expander, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            return EvaluateCondition(element.Condition, element, expanderOptions, parserOptions, expander, lazyEvaluator);
        }

        /// <summary>
        /// COMPAT: Whidbey used the "current project file/targets" directory for evaluating Import and PropertyGroup conditions
        /// Orcas broke this by using the current root project file for all conditions
        /// For Dev10+, we'll fix this, and use the current project file/targets directory for Import, ImportGroup and PropertyGroup
        /// but the root project file for the rest. Inside of targets will use the root project file as always.
        /// </summary>
        private static string GetCurrentDirectoryForConditionEvaluation(ProjectElement element, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            if (element is ProjectPropertyGroupElement || element is ProjectImportElement || element is ProjectImportGroupElement)
            {
                return element.ContainingProject.DirectoryPath;
            }
            else
            {
                return lazyEvaluator._outerEvaluatorData.Directory;
            }
        }

        public struct ItemData
        {
            public ItemData(I item, ProjectItemElement originatingItemElement, int elementOrder, bool conditionResult)
            {
                Item = item;
                OriginatingItemElement = originatingItemElement;
                ElementOrder = elementOrder;
                ConditionResult = conditionResult;
            }

            public ItemData Clone(IItemFactory<I, I> itemFactory, ProjectItemElement initialItemElementForFactory)
            {
                // setting the factory's item element to the original item element that produced the item
                // otherwise you get weird things like items that appear to have been produced by update elements
                itemFactory.ItemElement = OriginatingItemElement;
                var clonedItem = itemFactory.CreateItem(Item, OriginatingItemElement.ContainingProject.FullPath);
                itemFactory.ItemElement = initialItemElementForFactory;

                return new ItemData(clonedItem, OriginatingItemElement, ElementOrder, ConditionResult);
            }

            public I Item { get; }
            public ProjectItemElement OriginatingItemElement { get; }
            public int ElementOrder { get; }
            public bool ConditionResult { get; }
        }

        private class MemoizedOperation : IItemOperation
        {
            public IItemOperation Operation { get; }
            private Dictionary<ISet<string>, ImmutableList<ItemData>> _cache;

            private bool _isReferenced;
#if DEBUG
            private int _applyCalls;
#endif

            public MemoizedOperation(IItemOperation operation)
            {
                Operation = operation;
            }

            public void Apply(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
#if DEBUG
                CheckInvariant();
#endif

                Operation.Apply(listBuilder, globsToIgnore);

                // cache results if somebody is referencing this operation
                if (_isReferenced)
                {
                    AddItemsToCache(globsToIgnore, listBuilder.ToImmutable());
                }
#if DEBUG
                _applyCalls++;
                CheckInvariant();
#endif
            }

#if DEBUG
            private void CheckInvariant()
            {
                if (_isReferenced)
                {
                    var cacheCount = _cache?.Count ?? 0;
                    Debug.Assert(_applyCalls == cacheCount, "Apply should only be called once per globsToIgnore. Otherwise caching is not working");
                }
                else
                {
                    // non referenced operations should not be cached
                    // non referenced operations should have as many apply calls as the number of cache keys of the immediate dominator with _isReferenced == true
                    Debug.Assert(_cache == null);
                }
            }
#endif

            public bool TryGetFromCache(ISet<string> globsToIgnore, out ImmutableList<ItemData> items)
            {
                if (_cache != null)
                {
                    return _cache.TryGetValue(globsToIgnore, out items);
                }

                items = null;
                return false;
            }

            /// <summary>
            /// Somebody is referencing this operation
            /// </summary>
            public void MarkAsReferenced()
            {
                _isReferenced = true;
            }

            private void AddItemsToCache(ImmutableHashSet<string> globsToIgnore, ImmutableList<ItemData> items)
            {
                if (_cache == null)
                {
                    _cache = new Dictionary<ISet<string>, ImmutableList<ItemData>>();
                }

                _cache[globsToIgnore] = items;
            }

            private bool IsCached(ISet<string> globsToIgnore)
            {
                return _cache != null && _cache.ContainsKey(globsToIgnore);
            }
        }

        private class LazyItemList
        {
            private readonly LazyItemList _previous;
            private readonly MemoizedOperation _memoizedOperation;

            public LazyItemList(LazyItemList previous, LazyItemOperation operation)
            {
                _previous = previous;
                _memoizedOperation = new MemoizedOperation(operation);
            }

            public ImmutableList<I> GetMatchedItems(ImmutableHashSet<string> globsToIgnore)
            {
                ImmutableList<I>.Builder items = ImmutableList.CreateBuilder<I>();
                foreach (ItemData data in GetItemData(globsToIgnore))
                {
                    if (data.ConditionResult)
                        items.Add(data.Item);
                }

                return items.ToImmutable();
            }

            public ImmutableList<ItemData>.Builder GetItemData(ImmutableHashSet<string> globsToIgnore)
            {
                // Cache results only on the LazyItemOperations whose results are required by an external caller (via GetItems). This means:
                //   - Callers of GetItems who have announced ahead of time that they would reference an operation (via MarkAsReferenced())
                // This includes: item references (Include="@(foo)") and metadata conditions (Condition="@(foo->Count()) == 0")
                // Without ahead of time notifications more computation is done than needed when the results of a future operation are requested 
                // The future operation is part of another item list referencing this one (making this operation part of the tail).
                // The future operation will compute this list but since no ahead of time notifications have been made by callers, it won't cache the
                // intermediary operations that would be requested by those callers.
                //   - Callers of GetItems that cannot announce ahead of time. This includes item referencing conditions on
                // Item Groups and Item Elements. However, those conditions are performed eagerly outside of the LazyItemEvaluator, so they will run before
                // any item referencing operations from inside the LazyItemEvaluator. This 
                //
                // If the head of this LazyItemList is uncached, then the tail may contain cached and un-cached nodes.
                // In this case we have to compute the head plus the part of the tail up to the first cached operation.
                //
                // The cache is based on a couple of properties:
                // - uses immutable lists for structural sharing between multiple cached nodes (multiple include operations won't have duplicated memory for the common items)
                // - if an operation is cached for a certain set of globsToIgnore, then the entire operation tail can be reused. This is because (i) the structure of LazyItemLists
                // does not mutate: one can add operations on top, but the base never changes, and (ii) the globsToIgnore passed to the tail is the concatenation between
                // the globsToIgnore received as an arg, and the globsToIgnore produced by the head (if the head is a Remove operation)

                ImmutableList<ItemData> items;
                if (_memoizedOperation.TryGetFromCache(globsToIgnore, out items))
                {
                    return items.ToBuilder();
                }
                else
                {
                    // tell the cache that this operation's result is needed by an external caller
                    // this is required for callers that cannot tell the item list ahead of time that 
                    // they would be using an operation
                    MarkAsReferenced();

                    return ComputeItems(this, globsToIgnore);
                }

            }

            private static ImmutableList<ItemData>.Builder ComputeItems(LazyItemList lazyItemList, ImmutableHashSet<string> globsToIgnore)
            {
                // Stack of operations up to the first one that's cached (exclusive)
                Stack<LazyItemList> itemListStack = new Stack<LazyItemList>();

                ImmutableList<ItemData>.Builder items = null;

                // Keep a separate stack of lists of globs to ignore that only gets modified for Remove operations
                Stack<ImmutableHashSet<string>> globsToIgnoreStack = null;

                for (var currentList = lazyItemList; currentList != null; currentList = currentList._previous)
                {
                    var globsToIgnoreFromFutureOperations = globsToIgnoreStack?.Peek() ?? globsToIgnore;

                    ImmutableList<ItemData> itemsFromCache;
                    if (currentList._memoizedOperation.TryGetFromCache(globsToIgnoreFromFutureOperations, out itemsFromCache))
                    {
                        // the base items on top of which to apply the uncached operations are the items of the first operation that is cached
                        items = itemsFromCache.ToBuilder();
                        break;
                    }

                    //  If this is a remove operation, then add any globs that will be removed
                    //  to a list of globs to ignore in previous operations
                    var removeOperation = currentList._memoizedOperation.Operation as RemoveOperation;
                    if (removeOperation != null)
                    {
                        if (globsToIgnoreStack == null)
                        {
                            globsToIgnoreStack = new Stack<ImmutableHashSet<string>>();
                        }

                        var globsToIgnoreForPreviousOperations = removeOperation.GetRemovedGlobs();
                        foreach (var globToRemove in globsToIgnoreFromFutureOperations)
                        {
                            globsToIgnoreForPreviousOperations.Add(globToRemove);
                        }

                        globsToIgnoreStack.Push(globsToIgnoreForPreviousOperations.ToImmutable());
                    }

                    itemListStack.Push(currentList);
                }

                if (items == null)
                {
                    items = ImmutableList.CreateBuilder<ItemData>();
                }

                ImmutableHashSet<string> currentGlobsToIgnore = globsToIgnoreStack == null ? globsToIgnore : globsToIgnoreStack.Peek();

                //  Walk back down the stack of item lists applying operations
                while (itemListStack.Count > 0)
                {
                    var currentList = itemListStack.Pop();

                    //  If this is a remove operation, then it could modify the globs to ignore, so pop the potentially
                    //  modified entry off the stack of globs to ignore
                    var removeOperation = currentList._memoizedOperation.Operation as RemoveOperation;
                    if (removeOperation != null)
                    {
                        globsToIgnoreStack.Pop();
                        currentGlobsToIgnore = globsToIgnoreStack.Count == 0 ? globsToIgnore : globsToIgnoreStack.Peek();
                    }

                    currentList._memoizedOperation.Apply(items, currentGlobsToIgnore);
                }

                return items;
            }

            public void MarkAsReferenced()
            {
                _memoizedOperation.MarkAsReferenced();
            }
        }

        private class OperationBuilder
        {
            // WORKAROUND: Unnecessary boxed allocation: https://github.com/dotnet/corefx/issues/24563
            private static readonly ImmutableDictionary<string, LazyItemList> s_emptyIgnoreCase = ImmutableDictionary.Create<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

            public ProjectItemElement ItemElement { get; set; }
            public string ItemType { get; set; }
            public ItemSpec<P,I> ItemSpec { get; set; }

            public ImmutableDictionary<string, LazyItemList>.Builder ReferencedItemLists { get; } = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
                ImmutableDictionary.CreateBuilder<string, LazyItemList>() :
                s_emptyIgnoreCase.ToBuilder();

            public bool ConditionResult { get; set; }

            public OperationBuilder(ProjectItemElement itemElement, bool conditionResult)
            {
                ItemElement = itemElement;
                ItemType = itemElement.ItemType;
                ConditionResult = conditionResult;
            }
        }

        private class OperationBuilderWithMetadata : OperationBuilder
        {
            public ImmutableList<ProjectMetadataElement>.Builder Metadata = ImmutableList.CreateBuilder<ProjectMetadataElement>();

            public OperationBuilderWithMetadata(ProjectItemElement itemElement, bool conditionResult) : base(itemElement, conditionResult)
            {
            }
        }

        private void AddReferencedItemList(string itemType, IDictionary<string, LazyItemList> referencedItemLists)
        {
            var itemList = GetItemList(itemType);
            if (itemList != null)
            {
                itemList.MarkAsReferenced();
                referencedItemLists[itemType] = itemList;
            }
        }

        private LazyItemList GetItemList(string itemType)
        {
            LazyItemList ret;
            _itemLists.TryGetValue(itemType, out ret);
            return ret;
        }

        public IEnumerable<ItemData> GetAllItemsDeferred()
        {
            return _itemLists.Values.SelectMany(itemList => itemList.GetItemData(ImmutableHashSet<string>.Empty))
                                    .OrderBy(itemData => itemData.ElementOrder);
        }

        public void ProcessItemElement(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            LazyItemOperation operation = null;

            if (itemElement.IncludeLocation != null)
            {
                operation = BuildIncludeOperation(rootDirectory, itemElement, conditionResult);
            }
            else if (itemElement.RemoveLocation != null)
            {
                operation = BuildRemoveOperation(rootDirectory, itemElement, conditionResult);
            }
            else if (itemElement.UpdateLocation != null)
            {
                operation = BuildUpdateOperation(rootDirectory, itemElement, conditionResult);
            }
            else
            {
                ErrorUtilities.ThrowInternalErrorUnreachable();
            }

            LazyItemList previousItemList = GetItemList(itemElement.ItemType);
            LazyItemList newList = new LazyItemList(previousItemList, operation);
            _itemLists[itemElement.ItemType] = newList;
        }

        private UpdateOperation BuildUpdateOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            OperationBuilderWithMetadata operationBuilder = new OperationBuilderWithMetadata(itemElement, conditionResult);

            // Proces Update attribute
            ProcessItemSpec(rootDirectory, itemElement.Update, itemElement.UpdateLocation, operationBuilder);

            ProcessMetadataElements(itemElement, operationBuilder);

            return new UpdateOperation(operationBuilder, this);
        }

        private IncludeOperation BuildIncludeOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            IncludeOperationBuilder operationBuilder = new IncludeOperationBuilder(itemElement, conditionResult);
            operationBuilder.ElementOrder = _nextElementOrder++;
            operationBuilder.RootDirectory = rootDirectory;
            operationBuilder.ConditionResult = conditionResult;

            // Process include
            ProcessItemSpec(rootDirectory, itemElement.Include, itemElement.IncludeLocation, operationBuilder);

            //  Code corresponds to Evaluator.EvaluateItemElement

            // Process exclude (STEP 4: Evaluate, split, expand and subtract any Exclude)
            if (itemElement.Exclude.Length > 0)
            {
                //  Expand properties here, because a property may have a value which is an item reference (ie "@(Bar)"), and
                //  if so we need to add the right item reference
                string evaluatedExclude = _expander.ExpandIntoStringLeaveEscaped(itemElement.Exclude, ExpanderOptions.ExpandProperties, itemElement.ExcludeLocation);

                if (evaluatedExclude.Length > 0)
                {
                    var excludeSplits = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedExclude);

                    foreach (var excludeSplit in excludeSplits)
                    {
                        operationBuilder.Excludes.Add(excludeSplit);
                        AddItemReferences(excludeSplit, operationBuilder, itemElement.ExcludeLocation);
                    }
                }
            }

            // Process Metadata (STEP 5: Evaluate each metadata XML and apply them to each item we have so far)
            ProcessMetadataElements(itemElement, operationBuilder);

            return new IncludeOperation(operationBuilder, this);
        }

        private RemoveOperation BuildRemoveOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            OperationBuilder operationBuilder = new OperationBuilder(itemElement, conditionResult);

            ProcessItemSpec(rootDirectory, itemElement.Remove, itemElement.RemoveLocation, operationBuilder);

            return new RemoveOperation(operationBuilder, this);
        }

        private void ProcessItemSpec(string rootDirectory, string itemSpec, IElementLocation itemSpecLocation, OperationBuilder builder)
        {
            builder.ItemSpec = new ItemSpec<P, I>(itemSpec, _outerExpander, itemSpecLocation, rootDirectory);

            foreach (ItemFragment fragment in builder.ItemSpec.Fragments)
            {
                ItemExpressionFragment<P, I> itemExpression = fragment as ItemExpressionFragment<P, I>;
                if (itemExpression != null)
                {
                    AddReferencedItemLists(builder, itemExpression.Capture);
                }
            }
        }

        private void ProcessMetadataElements(ProjectItemElement itemElement, OperationBuilderWithMetadata operationBuilder)
        {
            if (itemElement.HasMetadata)
            {
                operationBuilder.Metadata.AddRange(itemElement.Metadata);

                var values = new List<string>(itemElement.Metadata.Count * 2);

                // Expand properties here, because a property may have a value which is an item reference (ie "@(Bar)"), and
                // if so we need to add the right item reference.
                foreach (var metadatumElement in itemElement.Metadata)
                {
                    var valueWithPropertiesExpanded = _expander.ExpandIntoStringLeaveEscaped(
                        metadatumElement.Value,
                        ExpanderOptions.ExpandProperties,
                        metadatumElement.Location);

                    var conditionWithPropertiesExpanded = _expander.ExpandIntoStringLeaveEscaped(
                        metadatumElement.Condition,
                        ExpanderOptions.ExpandProperties,
                        metadatumElement.ConditionLocation);

                    values.Add(valueWithPropertiesExpanded);
                    values.Add(conditionWithPropertiesExpanded);
                }

                var itemsAndMetadataFound = ExpressionShredder.GetReferencedItemNamesAndMetadata(values);
                if (itemsAndMetadataFound.Items != null)
                {
                    foreach (var itemType in itemsAndMetadataFound.Items)
                    {
                        AddReferencedItemList(itemType, operationBuilder.ReferencedItemLists);
                    }
                }
            }
        }

        private void AddItemReferences(string expression, IncludeOperationBuilder operationBuilder, IElementLocation elementLocation)
        {
            if (expression.Length == 0)
            {
                return;
            }
            else
            {
                ExpressionShredder.ItemExpressionCapture match = Expander<P, I>.ExpandSingleItemVectorExpressionIntoExpressionCapture(
                    expression, ExpanderOptions.ExpandItems, elementLocation);

                if (match == null)
                {
                    return;
                }

                AddReferencedItemLists(operationBuilder, match);
            }
        }

        private void AddReferencedItemLists(OperationBuilder operationBuilder, ExpressionShredder.ItemExpressionCapture match)
        {
            if (match.ItemType != null)
            {
                AddReferencedItemList(match.ItemType, operationBuilder.ReferencedItemLists);
            }
            if (match.Captures != null)
            {
                foreach (var subMatch in match.Captures)
                {
                    AddReferencedItemLists(operationBuilder, subMatch);
                }
            }
        }
    }
}
