// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

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

        private int _nextElementOrder = 0;

        /// <summary>
        /// Build event context to log evaluator events in.
        /// </summary>
        private BuildEventContext _buildEventContext = null;

        /// <summary>
        /// The logging service for use during evaluation
        /// </summary>
        private readonly ILoggingService _loggingService;

        /// <summary>
        /// The CultureInfo from the invariant culture. Used to avoid allocations for
        /// perfoming IndexOf etc.
        /// </summary>
        private static CompareInfo s_invariantCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

        private Dictionary<string, LazyItemList> _itemLists = new Dictionary<string, LazyItemList>();

        public LazyItemEvaluator(IEvaluatorData<P, I, M, D> data, IItemFactory<I, I> itemFactory, BuildEventContext buildEventContext, ILoggingService loggingService)
        {
            _outerEvaluatorData = data;
            _outerExpander = new Expander<P, I>(_outerEvaluatorData, _outerEvaluatorData);
            _evaluatorData = new EvaluatorData(_outerEvaluatorData, itemType => GetItems(itemType).Select(itemData => itemData.Item).ToList());
            _expander = new Expander<P, I>(_evaluatorData, _evaluatorData);
            _itemFactory = itemFactory;

            _buildEventContext = buildEventContext;
            _loggingService = loggingService;
        }

        private ICollection<ItemData> GetItems(string itemType)
        {
            LazyItemList itemList = GetItemList(itemType);
            if (itemList == null)
            {
                return ImmutableList<ItemData>.Empty;
            }
            return itemList.GetItems(ImmutableHashSet<string>.Empty).Where(itemData => itemData.ConditionResult).ToList();
        }

        public bool EvaluateConditionWithCurrentState(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        {
            return EvaluateCondition(element, expanderOptions, parserOptions, _expander, this);
        }

        private static bool EvaluateCondition(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions, Expander<P, I> expander, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            if (element.Condition.Length == 0)
            {
                return true;
            }

            bool result = ConditionEvaluator.EvaluateCondition
                (
                element.Condition,
                parserOptions,
                expander,
                expanderOptions,
                GetCurrentDirectoryForConditionEvaluation(element, lazyEvaluator),
                element.ConditionLocation,
                lazyEvaluator._loggingService,
                lazyEvaluator._buildEventContext
                );

            return result;

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
            private I _item;
            private int _elementOrder;
            private bool _conditionResult;

            public ItemData(I item, int elementOrder, bool conditionResult)
            {
                _item = item;
                _elementOrder = elementOrder;
                _conditionResult = conditionResult;
            }

            public I Item { get { return _item; } }
            public int ElementOrder { get { return _elementOrder; } }
            public bool ConditionResult { get { return _conditionResult; } }
        }


        private class LazyItemList
        {
            private readonly LazyItemList _previous;
            private readonly LazyItemOperation _operation;

            public LazyItemList(LazyItemList previous, LazyItemOperation operation)
            {
                _previous = previous;
                _operation = operation;
            }

            public ImmutableList<ItemData>.Builder GetItems(ImmutableHashSet<string> globsToIgnore)
            {
                //  TODO: Check for cached results

                return GetItemsImplementation(this, globsToIgnore);
            }

            static ImmutableList<ItemData>.Builder GetItemsImplementation(LazyItemList lazyItemList, ImmutableHashSet<string> globsToIgnore)
            {
                Stack<LazyItemList> itemListStack = new Stack<LazyItemList>();

                //  Keep a separate stack of lists of globs to ignore that only gets modified for Remove operations
                Stack<ImmutableHashSet<string>> globsToIgnoreStack = null;

                for (var currentList = lazyItemList; currentList != null; currentList = currentList._previous)
                {
                    //  If this is a remove operation, then add any globs that will be removed
                    //  to a list of globs to ignore in previous operations
                    var removeOperation = currentList._operation as RemoveOperation;
                    if (removeOperation != null)
                    {
                        if (globsToIgnoreStack == null)
                        {
                            globsToIgnoreStack = new Stack<ImmutableHashSet<string>>();
                        }

                        var globsToIgnoreFromFutureOperations = globsToIgnoreStack.Count > 0 ? globsToIgnoreStack.Peek() : globsToIgnore;

                        var globsToIgnoreForPreviousOperations = removeOperation.GetRemovedGlobs();
                        foreach (var globToRemove in globsToIgnoreFromFutureOperations)
                        {
                            globsToIgnoreForPreviousOperations.Add(globToRemove);
                        }

                        globsToIgnoreStack.Push(globsToIgnoreForPreviousOperations.ToImmutable());
                    }

                    itemListStack.Push(currentList);
                }

                ImmutableList<ItemData>.Builder items = ImmutableList.CreateBuilder<ItemData>();
                ImmutableHashSet<string> currentGlobsToIgnore = globsToIgnoreStack == null ? globsToIgnore : globsToIgnoreStack.Peek();

                //  Walk back down the stack of item lists applying operations
                while (itemListStack.Count > 0)
                {
                    var currentList = itemListStack.Pop();

                    //  If this is a remove operation, then it could modify the globs to ignore, so pop the potentially
                    //  modified entry off the stack of globs to ignore
                    var removeOperation = currentList._operation as RemoveOperation;
                    if (removeOperation != null)
                    {
                        globsToIgnoreStack.Pop();
                        currentGlobsToIgnore = globsToIgnoreStack.Count == 0 ? globsToIgnore : globsToIgnoreStack.Peek();
                    }

                    currentList._operation.Apply(items, currentGlobsToIgnore);
                    //  TODO: Cache result of operation (possibly only if it involved executing globs)
                }

                return items;
            }
        }

        private class OperationBuilder
        {
            public ProjectItemElement ItemElement { get; set; }
            public string ItemType { get; set; }
            public ItemSpec<P,I> ItemSpec { get; set; }

            public ImmutableDictionary<string, LazyItemList>.Builder ReferencedItemLists { get; } = ImmutableDictionary.CreateBuilder<string, LazyItemList>();

            public OperationBuilder(ProjectItemElement itemElement)
            {
                ItemElement = itemElement;
                ItemType = itemElement.ItemType;
            }
        }

        private class OperationBuilderWithMetadata : OperationBuilder
        {
            public ImmutableList<ProjectMetadataElement>.Builder Metadata = ImmutableList.CreateBuilder<ProjectMetadataElement>();

            public OperationBuilderWithMetadata(ProjectItemElement itemElement) : base(itemElement)
            {
            }
        }

        private LazyItemList GetItemList(string itemType)
        {
            LazyItemList ret;
            _itemLists.TryGetValue(itemType, out ret);
            return ret;
        }

        public IList<ItemData> GetAllItems()
        {
            var ret = _itemLists.Values.SelectMany(itemList => itemList.GetItems(ImmutableHashSet<string>.Empty))
                .OrderBy(itemData => itemData.ElementOrder)
                .ToList();

            return ret;
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
                operation = BuildRemoveOperation(rootDirectory, itemElement);
            }
            else if (itemElement.UpdateLocation != null)
            {
                operation = BuildUpdateOperation(rootDirectory, itemElement);
            }
            else
            {
                ErrorUtilities.ThrowInternalErrorUnreachable();
            }

            LazyItemList previousItemList = GetItemList(itemElement.ItemType);
            LazyItemList newList = new LazyItemList(previousItemList, operation);
            _itemLists[itemElement.ItemType] = newList;
        }

        private UpdateOperation BuildUpdateOperation(string rootDirectory, ProjectItemElement itemElement)
        {
            OperationBuilderWithMetadata operationBuilder = new OperationBuilderWithMetadata(itemElement);

            // Proces Update attribute
            ProcessItemSpec(itemElement.Update, itemElement.UpdateLocation, operationBuilder);

            ProcessMetadataElements(itemElement, operationBuilder);

            return new UpdateOperation(operationBuilder, this);
        }

        private IncludeOperation BuildIncludeOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            IncludeOperationBuilder operationBuilder = new IncludeOperationBuilder(itemElement);
            operationBuilder.ElementOrder = _nextElementOrder++;
            operationBuilder.RootDirectory = rootDirectory;
            operationBuilder.ConditionResult = conditionResult;

            // Process include
            ProcessItemSpec(itemElement.Include, itemElement.IncludeLocation, operationBuilder);

            //  Code corresponds to Evaluator.EvaluateItemElement

            // Process exclude (STEP 4: Evaluate, split, expand and subtract any Exclude)
            if (itemElement.Exclude.Length > 0)
            {
                //  Expand properties here, because a property may have a value which is an item reference (ie "@(Bar)"), and
                //  if so we need to add the right item reference
                string evaluatedExclude = _expander.ExpandIntoStringLeaveEscaped(itemElement.Exclude, ExpanderOptions.ExpandProperties, itemElement.ExcludeLocation);

                if (evaluatedExclude.Length > 0)
                {
                    IList<string> excludeSplits = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedExclude);

                    operationBuilder.Excludes.AddRange(excludeSplits);

                    foreach (var excludeSplit in excludeSplits)
                    {
                        AddItemReferences(excludeSplit, operationBuilder, itemElement.ExcludeLocation);
                    }
                }
            }

            // Process Metadata (STEP 5: Evaluate each metadata XML and apply them to each item we have so far)
            ProcessMetadataElements(itemElement, operationBuilder);

            return new IncludeOperation(operationBuilder, this);
        }

        private RemoveOperation BuildRemoveOperation(string rootDirectory, ProjectItemElement itemElement)
        {
            OperationBuilder operationBuilder = new OperationBuilder(itemElement);

            ProcessItemSpec(itemElement.Remove, itemElement.RemoveLocation, operationBuilder);

            return new RemoveOperation(operationBuilder, this);
        }

        private void ProcessItemSpec(string itemSpec, IElementLocation itemSpecLocation, OperationBuilder builder)
        {
            builder.ItemSpec = new ItemSpec<P, I>(itemSpec, _outerExpander, itemSpecLocation);

            var itemCaptures = builder.ItemSpec.Fragments.OfType<ItemExpressionFragment<P, I>>().Select(i => i.Capture);
            AddReferencedItemLists(builder, itemCaptures);
        }

        private void ProcessMetadataElements(ProjectItemElement itemElement, OperationBuilderWithMetadata operationBuilder)
        {
            if (itemElement.HasMetadata)
            {
                operationBuilder.Metadata.AddRange(itemElement.Metadata);

                List<string> values = new List<string>(itemElement.Metadata.Count * 2);

                foreach (ProjectMetadataElement metadatumElement in itemElement.Metadata)
                {
                    values.Add(metadatumElement.Value);
                    values.Add(metadatumElement.Condition);
                }

                ItemsAndMetadataPair itemsAndMetadataFound = ExpressionShredder.GetReferencedItemNamesAndMetadata(values);
                if (itemsAndMetadataFound.Items != null)
                {
                    foreach (var itemType in itemsAndMetadataFound.Items)
                    {
                        var itemList = GetItemList(itemType);
                        if (itemList != null)
                        {
                            operationBuilder.ReferencedItemLists[itemType] = itemList;
                        }
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

        private void AddReferencedItemLists(OperationBuilder operationBuilder, IEnumerable<ExpressionShredder.ItemExpressionCapture> captures)
        {
            foreach (var capture in captures)
            {
                AddReferencedItemLists(operationBuilder, capture);
            }
        }

        private void AddReferencedItemLists(OperationBuilder operationBuilder, ExpressionShredder.ItemExpressionCapture match)
        {
            if (match.ItemType != null)
            {
                var itemList = GetItemList(match.ItemType);
                if (itemList != null)
                {
                    operationBuilder.ReferencedItemLists[match.ItemType] = itemList;
                }
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
