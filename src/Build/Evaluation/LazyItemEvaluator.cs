// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Eventing;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
        private readonly LoggingContext _loggingContext;
        private readonly EvaluationProfiler _evaluationProfiler;

        private int _nextElementOrder = 0;

        private Dictionary<string, LazyItemList> _itemLists = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
            new Dictionary<string, LazyItemList>() :
            new Dictionary<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

        protected IFileSystem FileSystem { get; }

        protected EngineFileUtilities EngineFileUtilities { get; }

        public LazyItemEvaluator(IEvaluatorData<P, I, M, D> data, IItemFactory<I, I> itemFactory, LoggingContext loggingContext, EvaluationProfiler evaluationProfiler, EvaluationContext evaluationContext)
        {
            _outerEvaluatorData = data;
            _outerExpander = new Expander<P, I>(_outerEvaluatorData, _outerEvaluatorData, evaluationContext.FileSystem);
            _evaluatorData = new EvaluatorData(_outerEvaluatorData, itemType => GetItems(itemType));
            _expander = new Expander<P, I>(_evaluatorData, _evaluatorData, evaluationContext.FileSystem);
            _itemFactory = itemFactory;
            _loggingContext = loggingContext;
            _evaluationProfiler = evaluationProfiler;

            FileSystem = evaluationContext.FileSystem;
            EngineFileUtilities = evaluationContext.EngineFileUtilities;
        }

        private ImmutableList<I> GetItems(string itemType)
        {
            return _itemLists.TryGetValue(itemType, out LazyItemList itemList) ?
                itemList.GetMatchedItems(ImmutableHashSet<string>.Empty) :
                ImmutableList<I>.Empty;
        }

        public bool EvaluateConditionWithCurrentState(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        {
            return EvaluateCondition(element.Condition, element, expanderOptions, parserOptions, _expander, this);
        }

        private static bool EvaluateCondition(
            string condition,
            ProjectElement element,
            ExpanderOptions expanderOptions,
            ParserOptions parserOptions,
            Expander<P, I> expander,
            LazyItemEvaluator<P, I, M, D> lazyEvaluator
            )
        {
            if (condition?.Length == 0)
            {
                return true;
            }
            MSBuildEventSource.Log.EvaluateConditionStart(condition);

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
                    lazyEvaluator._loggingContext.BuildEventContext,
                    lazyEvaluator.FileSystem
                    );
                MSBuildEventSource.Log.EvaluateConditionStop(condition, result);

                return result;
            }
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
            public LazyItemOperation Operation { get; }
            private Dictionary<ISet<string>, OrderedItemDataCollection> _cache;

            private bool _isReferenced;
#if DEBUG
            private int _applyCalls;
#endif

            public MemoizedOperation(LazyItemOperation operation)
            {
                Operation = operation;
            }

            public void Apply(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
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

            public bool TryGetFromCache(ISet<string> globsToIgnore, out OrderedItemDataCollection items)
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

            private void AddItemsToCache(ImmutableHashSet<string> globsToIgnore, OrderedItemDataCollection items)
            {
                if (_cache == null)
                {
                    _cache = new Dictionary<ISet<string>, OrderedItemDataCollection>();
                }

                _cache[globsToIgnore] = items;
            }
        }

        /// <summary>
        /// A collection of ItemData that maintains insertion order and internally optimizes some access patterns, e.g. bulk removal
        /// based on normalized item values.
        /// </summary>
        internal sealed class OrderedItemDataCollection
        {
            #region Inner types

            /// <summary>
            /// An efficient multi-value wrapper holding one or more items.
            /// </summary>
            internal struct DictionaryValue
            {
                /// <summary>
                /// A non-allocating enumerator for the multi-value.
                /// </summary>
                public struct Enumerator : IEnumerator<I>
                {
                    private object _value;
                    private int _index;

                    public Enumerator(object value)
                    {
                        _value = value;
                        _index = -1;
                    }

                    public I Current => (_value is IList<I> list) ? list[_index] : (I)_value;
                    object System.Collections.IEnumerator.Current => Current;

                    public void Dispose()
                    { }

                    public bool MoveNext()
                    {
                        int count = (_value is IList<I> list) ? list.Count : 1;
                        if (_index + 1 < count)
                        {
                            _index++;
                            return true;
                        }
                        return false;
                    }

                    public void Reset()
                    {
                        _index = -1;
                    }
                }

                /// <summary>
                /// Holds one value or a list of values.
                /// </summary>
                private object _value;

                public DictionaryValue(I item)
                {
                    _value = item;
                }

                public void Add(I item)
                {
                    if (_value is not ImmutableList<I> list)
                    {
                        list = ImmutableList<I>.Empty;
                        list = list.Add((I)_value);
                    }
                    _value = list.Add(item);
                }

                public Enumerator GetEnumerator()
                {
                    return new Enumerator(_value);
                }
            }

            /// <summary>
            /// A mutable and enumerable version of <see cref="OrderedItemDataCollection"/>.
            /// </summary>
            internal sealed class Builder : IEnumerable<ItemData>
            {
                /// <summary>
                /// The list of items in the collection. Defines the enumeration order.
                /// </summary>
                private ImmutableList<ItemData>.Builder _listBuilder;

                /// <summary>
                /// A dictionary of items keyed by their normalized value.
                /// </summary>
                private ImmutableDictionary<string, DictionaryValue>.Builder _dictionaryBuilder;

                internal Builder(ImmutableList<ItemData>.Builder listBuilder, ImmutableDictionary<string, DictionaryValue>.Builder dictionaryBuilder)
                {
                    _listBuilder = listBuilder;
                    _dictionaryBuilder = dictionaryBuilder;
                }

                #region IEnumerable implementation

                ImmutableList<ItemData>.Enumerator GetEnumerator() => _listBuilder.GetEnumerator();
                IEnumerator<ItemData> IEnumerable<ItemData>.GetEnumerator() => _listBuilder.GetEnumerator();

                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _listBuilder.GetEnumerator();

                #endregion

                public int Count => _listBuilder.Count;

                public ItemData this[int index]
                {
                    get { return _listBuilder[index]; }
                    set
                    {
                        _listBuilder[index] = value;
                        // This is a rare operation, don't bother updating the dictionary for now. It will be recreated as needed.
                        _dictionaryBuilder = null;
                    }
                }

                public void Add(ItemData data)
                {
                    _listBuilder.Add(data);
                    if (_dictionaryBuilder != null)
                    {
                        AddToDictionary(data.Item);
                    }
                }

                public void Clear()
                {
                    _listBuilder.Clear();
                    _dictionaryBuilder?.Clear();
                }

                public void RemoveAll(ICollection<I> itemsToRemove)
                {
                    _listBuilder.RemoveAll(item => itemsToRemove.Contains(item.Item));
                    // This is a rare operation, don't bother updating the dictionary for now. It will be recreated as needed.
                    _dictionaryBuilder = null;
                }

                /// <summary>
                /// Removes items from the collection that match the given ItemSpec.
                /// </summary>
                /// <remarks>
                /// If <see cref="_dictionaryBuilder"/> does not exist yet, it is created in this method to avoid the cost of comparing each item
                /// being removed with each item already in the collection. The dictionary is kept in sync with the <see cref="_listBuilder"/>
                /// as long as practical. If an operation would result in too much of such work, the dictionary is simply dropped and recreated
                /// later if/when needed.
                /// </remarks>
                public void RemoveMatchingItems(ItemSpec<P, I> itemSpec)
                {
                    HashSet<I> items = null;
                    var dictionaryBuilder = GetOrCreateDictionaryBuilder();
                    foreach (var fragment in itemSpec.Fragments)
                    {
                        IEnumerable<string> referencedItems = fragment.GetReferencedItems();
                        if (referencedItems != null)
                        {
                            // The fragment can enumerate its referenced items, we can do dictionary lookups.
                            foreach (var spec in referencedItems)
                            {
                                string key = FileUtilities.NormalizePathForComparisonNoThrow(spec, fragment.ProjectDirectory);
                                if (dictionaryBuilder.TryGetValue(key, out var multiValue))
                                {
                                    items ??= new HashSet<I>();
                                    foreach (I item in multiValue)
                                    {
                                        items.Add(item);
                                    }
                                    dictionaryBuilder.Remove(key);
                                }
                            }
                        }
                        else
                        {
                            // The fragment cannot enumerate its referenced items. Iterate over the dictionary and test each item.
                            List<string> keysToRemove = null;
                            foreach (var kvp in dictionaryBuilder)
                            {
                                if (fragment.IsMatchNormalized(kvp.Key))
                                {
                                    items ??= new HashSet<I>();
                                    foreach (I item in kvp.Value)
                                    {
                                        items.Add(item);
                                    }
                                    keysToRemove ??= new List<string>();
                                    keysToRemove.Add(kvp.Key);
                                }
                            }

                            if (keysToRemove != null)
                            {
                                foreach (string key in keysToRemove)
                                {
                                    dictionaryBuilder.Remove(key);
                                }
                            }
                        }
                    }

                    // Finish by removing items from the list.
                    if (items != null)
                    {
                        _listBuilder.RemoveAll(item => items.Contains(item.Item));
                    }
                }

                /// <summary>
                /// Creates an immutable view of this collection.
                /// </summary>
                public OrderedItemDataCollection ToImmutable()
                {
                    return new OrderedItemDataCollection(_listBuilder.ToImmutable(), _dictionaryBuilder?.ToImmutable());
                }

                private IDictionary<string, DictionaryValue> GetOrCreateDictionaryBuilder()
                {
                    if (_dictionaryBuilder == null)
                    {
                        _dictionaryBuilder = ImmutableDictionary.CreateBuilder<string, DictionaryValue>(StringComparer.OrdinalIgnoreCase);
                        foreach (ItemData item in _listBuilder)
                        {
                            AddToDictionary(item.Item);
                        }
                    }
                    return _dictionaryBuilder;
                }

                private void AddToDictionary(I item)
                {
                    string key = FileUtilities.NormalizePathForComparisonNoThrow(item.EvaluatedInclude, item.ProjectDirectory);

                    if (!_dictionaryBuilder.TryGetValue(key, out var dictionaryValue))
                    {
                        dictionaryValue = new DictionaryValue(item);
                    }
                    else
                    {
                        dictionaryValue.Add(item);
                    }
                    _dictionaryBuilder[key] = dictionaryValue;
                }
            }

            #endregion

            /// <summary>
            /// The list of items in the collection. Defines the enumeration order.
            /// </summary>
            private ImmutableList<ItemData> _list;

            /// <summary>
            /// A dictionary of items keyed by their normalized value.
            /// </summary>
            private ImmutableDictionary<string, DictionaryValue> _dictionary;

            private OrderedItemDataCollection(ImmutableList<ItemData> list, ImmutableDictionary<string, DictionaryValue> dictionary)
            {
                _list = list;
                _dictionary = dictionary;
            }

            /// <summary>
            /// Creates a new mutable collection.
            /// </summary>
            public static Builder CreateBuilder()
            {
                return new Builder(ImmutableList.CreateBuilder<ItemData>(), null);
            }

            /// <summary>
            /// Creates a mutable view of this collection. Changes made to the returned builder are not reflected in this collection.
            /// </summary>
            public Builder ToBuilder()
            {
                return new Builder(_list.ToBuilder(), _dictionary?.ToBuilder());
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

            public OrderedItemDataCollection.Builder GetItemData(ImmutableHashSet<string> globsToIgnore)
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

                OrderedItemDataCollection items;
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

            /// <summary>
            /// Applies uncached item operations (include, remove, update) in order. Since Remove effectively overwrites Include or Update,
            /// Remove operations are preprocessed (adding to globsToIgnore) to create a longer list of globs we don't need to process
            /// properly because we know they will be removed. Update operations are batched as much as possible, meaning rather
            /// than being applied immediately, they are combined into a dictionary of UpdateOperations that need to be applied. This
            /// is to optimize the case in which as series of UpdateOperations, each of which affects a single ItemSpec, are applied to all
            /// items in the list, leading to a quadratic-time operation.
            /// </summary>
            private static OrderedItemDataCollection.Builder ComputeItems(LazyItemList lazyItemList, ImmutableHashSet<string> globsToIgnore)
            {
                // Stack of operations up to the first one that's cached (exclusive)
                Stack<LazyItemList> itemListStack = new Stack<LazyItemList>();

                OrderedItemDataCollection.Builder items = null;

                // Keep a separate stack of lists of globs to ignore that only gets modified for Remove operations
                Stack<ImmutableHashSet<string>> globsToIgnoreStack = null;

                for (var currentList = lazyItemList; currentList != null; currentList = currentList._previous)
                {
                    var globsToIgnoreFromFutureOperations = globsToIgnoreStack?.Peek() ?? globsToIgnore;

                    OrderedItemDataCollection itemsFromCache;
                    if (currentList._memoizedOperation.TryGetFromCache(globsToIgnoreFromFutureOperations, out itemsFromCache))
                    {
                        // the base items on top of which to apply the uncached operations are the items of the first operation that is cached
                        items = itemsFromCache.ToBuilder();
                        break;
                    }

                    //  If this is a remove operation, then add any globs that will be removed
                    //  to a list of globs to ignore in previous operations
                    if (currentList._memoizedOperation.Operation is RemoveOperation removeOperation)
                    {
                        globsToIgnoreStack ??= new Stack<ImmutableHashSet<string>>();

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
                    items = OrderedItemDataCollection.CreateBuilder();
                }

                ImmutableHashSet<string> currentGlobsToIgnore = globsToIgnoreStack == null ? globsToIgnore : globsToIgnoreStack.Peek();

                Dictionary<string, UpdateOperation> itemsWithNoWildcards = new Dictionary<string, UpdateOperation>(StringComparer.OrdinalIgnoreCase);
                bool addedToBatch = false;

                //  Walk back down the stack of item lists applying operations
                while (itemListStack.Count > 0)
                {
                    var currentList = itemListStack.Pop();

                    if (currentList._memoizedOperation.Operation is UpdateOperation op)
                    {
                        bool addToBatch = true;
                        int i;
                        // The TextFragments are things like abc.def or x*y.*z.
                        for (i = 0; i < op.Spec.Fragments.Count; i++)
                        {
                            ItemSpecFragment frag = op.Spec.Fragments[i];
                            if (MSBuildConstants.CharactersForExpansion.Any(frag.TextFragment.Contains))
                            {
                                // Fragment contains wild cards, items, or properties. Cannot batch over it using a dictionary.
                                addToBatch = false;
                                break;
                            }

                            string fullPath = FileUtilities.GetFullPath(frag.TextFragment, frag.ProjectDirectory);
                            if (itemsWithNoWildcards.ContainsKey(fullPath))
                            {
                                // Another update will already happen on this path. Make that happen before evaluating this one.
                                addToBatch = false;
                                break;
                            }
                            else
                            {
                                itemsWithNoWildcards.Add(fullPath, op);
                            }
                        }
                        if (!addToBatch)
                        {
                            // We found a wildcard. Remove any fragments associated with the current operation and process them later.
                            for (int j = 0; j < i; j++)
                            {
                                itemsWithNoWildcards.Remove(currentList._memoizedOperation.Operation.Spec.Fragments[j].TextFragment);
                            }
                        }
                        else
                        {
                            addedToBatch = true;
                            continue;
                        }
                    }

                    if (addedToBatch)
                    {
                        addedToBatch = false;
                        ProcessNonWildCardItemUpdates(itemsWithNoWildcards, items);
                    }

                    //  If this is a remove operation, then it could modify the globs to ignore, so pop the potentially
                    //  modified entry off the stack of globs to ignore
                    if (currentList._memoizedOperation.Operation is RemoveOperation)
                    {
                        globsToIgnoreStack.Pop();
                        currentGlobsToIgnore = globsToIgnoreStack.Count == 0 ? globsToIgnore : globsToIgnoreStack.Peek();
                    }

                    currentList._memoizedOperation.Apply(items, currentGlobsToIgnore);
                }

                // We finished looping through the operations. Now process the final batch if necessary.
                ProcessNonWildCardItemUpdates(itemsWithNoWildcards, items);

                return items;
            }

            private static void ProcessNonWildCardItemUpdates(Dictionary<string, UpdateOperation> itemsWithNoWildcards, OrderedItemDataCollection.Builder items)
            {
#if DEBUG
                ErrorUtilities.VerifyThrow(itemsWithNoWildcards.All(fragment => !MSBuildConstants.CharactersForExpansion.Any(fragment.Key.Contains)), $"{nameof(itemsWithNoWildcards)} should not contain any text fragments with wildcards.");
#endif
                if (itemsWithNoWildcards.Count > 0)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (itemsWithNoWildcards.TryGetValue(FileUtilities.GetFullPath(items[i].Item.EvaluatedInclude, items[i].Item.ProjectDirectory), out UpdateOperation op))
                        {
                            items[i] = op.UpdateItem(items[i]);
                        }
                    }
                    itemsWithNoWildcards.Clear();
                }
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
            if (_itemLists.TryGetValue(itemType, out LazyItemList itemList))
            {
                itemList.MarkAsReferenced();
                referencedItemLists[itemType] = itemList;
            }
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

            _itemLists.TryGetValue(itemElement.ItemType, out LazyItemList previousItemList);
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
            RemoveOperationBuilder operationBuilder = new RemoveOperationBuilder(itemElement, conditionResult);

            ProcessItemSpec(rootDirectory, itemElement.Remove, itemElement.RemoveLocation, operationBuilder);

            // Process MatchOnMetadata
            if (itemElement.MatchOnMetadata.Length > 0)
            {
                string evaluatedmatchOnMetadata = _expander.ExpandIntoStringLeaveEscaped(itemElement.MatchOnMetadata, ExpanderOptions.ExpandProperties, itemElement.MatchOnMetadataLocation);

                if (evaluatedmatchOnMetadata.Length > 0)
                {
                    var matchOnMetadataSplits = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedmatchOnMetadata);

                    foreach (var matchOnMetadataSplit in matchOnMetadataSplits)
                    {
                        AddItemReferences(matchOnMetadataSplit, operationBuilder, itemElement.MatchOnMetadataLocation);
                        string metadataExpanded = _expander.ExpandIntoStringLeaveEscaped(matchOnMetadataSplit, ExpanderOptions.ExpandPropertiesAndItems, itemElement.MatchOnMetadataLocation);
                        var metadataSplits = ExpressionShredder.SplitSemiColonSeparatedList(metadataExpanded);
                        operationBuilder.MatchOnMetadata.AddRange(metadataSplits);
                    }
                }
            }

            operationBuilder.MatchOnMetadataOptions = MatchOnMetadataOptions.CaseSensitive;
            if (Enum.TryParse(itemElement.MatchOnMetadataOptions, out MatchOnMetadataOptions options))
            {
                operationBuilder.MatchOnMetadataOptions = options;
            }

            return new RemoveOperation(operationBuilder, this);
        }

        private void ProcessItemSpec(string rootDirectory, string itemSpec, IElementLocation itemSpecLocation, OperationBuilder builder)
        {
            builder.ItemSpec = new ItemSpec<P, I>(itemSpec, _outerExpander, itemSpecLocation, rootDirectory);

            foreach (ItemSpecFragment fragment in builder.ItemSpec.Fragments)
            {
                if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemExpression)
                {
                    AddReferencedItemLists(builder, itemExpression.Capture);
                }
            }
        }

        private static IEnumerable<string> GetExpandedMetadataValuesAndConditions(ICollection<ProjectMetadataElement> metadata, Expander<P, I> expander)
        {
            // Since we're just attempting to expand properties in order to find referenced items and not expanding metadata,
            // unexpected errors may occur when evaluating property functions on unexpanded metadata. Just ignore them if that happens.
            // See: https://github.com/Microsoft/msbuild/issues/3460
            const ExpanderOptions expanderOptions = ExpanderOptions.ExpandProperties | ExpanderOptions.LeavePropertiesUnexpandedOnError;

            // Expand properties here, because a property may have a value which is an item reference (ie "@(Bar)"), and
            // if so we need to add the right item reference.
            foreach (var metadatumElement in metadata)
            {
                yield return expander.ExpandIntoStringLeaveEscaped(
                    metadatumElement.Value,
                    expanderOptions,
                    metadatumElement.Location);

                yield return expander.ExpandIntoStringLeaveEscaped(
                    metadatumElement.Condition,
                    expanderOptions,
                    metadatumElement.ConditionLocation);
            }
        }

        private void ProcessMetadataElements(ProjectItemElement itemElement, OperationBuilderWithMetadata operationBuilder)
        {
            if (itemElement.HasMetadata)
            {
                operationBuilder.Metadata.AddRange(itemElement.Metadata);

                var itemsAndMetadataFound = ExpressionShredder.GetReferencedItemNamesAndMetadata(GetExpandedMetadataValuesAndConditions(itemElement.Metadata, _expander));
                if (itemsAndMetadataFound.Items != null)
                {
                    foreach (var itemType in itemsAndMetadataFound.Items)
                    {
                        AddReferencedItemList(itemType, operationBuilder.ReferencedItemLists);
                    }
                }
            }
        }

        private void AddItemReferences(string expression, OperationBuilder operationBuilder, IElementLocation elementLocation)
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
