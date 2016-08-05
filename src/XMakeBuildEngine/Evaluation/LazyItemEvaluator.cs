using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Debugging;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        Dictionary<string, LazyItemList> _itemLists = new Dictionary<string, LazyItemList>();

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

        ICollection<ItemData> GetItems(string itemType)
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

        static bool EvaluateCondition(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions, Expander<P, I> expander, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
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
            I _item;
            int _elementOrder;
            bool _conditionResult;

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


        class LazyItemList
        {
            readonly LazyItemList _previous;
            readonly LazyItemOperation _operation;

            public LazyItemList(LazyItemList previous, LazyItemOperation operation)
            {
                _previous = previous;
                _operation = operation;
            }

            public ImmutableList<ItemData>.Builder GetItems(ImmutableHashSet<string> globsToIgnore)
            {
                ImmutableList<ItemData>.Builder items;
                if (_previous == null)
                {
                    items = ImmutableList.CreateBuilder<ItemData>();
                }
                else
                {
                    //  TODO: remove operation should modify globsToIgnore
                    items = _previous.GetItems(globsToIgnore);
                }

                _operation.Apply(items, globsToIgnore);

                //  TODO: cache result if any globs were executed

                return items;
            }
        }

        //  Operations:
        //  Add
        //      Previous value
        //      List<string> globs
        //      List<string> values
        //      List<LazyItemList> itemLists
        //      List<string> excludes
        //  Remove
        //      Previous value
        //      List<string> globsToRemove
        //      List<string> valuesToRemove
        //      List<LazyItemList> itemListsToRemove
        //  Update - ???

        abstract class LazyItemOperation
        {
            public abstract void Apply(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore);
        }

        enum IncludeOperationType
        {
            //  Values are escaped
            Value,
            //  Globs are not escaped
            Glob,
            Expression
        }

        class IncludeOperation : LazyItemOperation
        {
            readonly int _elementOrder;
            readonly ProjectItemElement _itemElement;
            readonly string _rootDirectory;
            readonly string _itemType;
            readonly bool _conditionResult;

            readonly ImmutableList<Tuple<IncludeOperationType, object>> _operations;

            readonly ImmutableDictionary<string, LazyItemList> _referencedItemLists;

            readonly ImmutableList<string> _excludes;

            readonly ImmutableList<ProjectMetadataElement> _metadata;

            readonly LazyItemEvaluator<P, I, M, D> _lazyEvaluator;
            readonly EvaluatorData _evaluatorData;
            readonly Expander<P, I> _expander;
            readonly IItemFactory<I, I> _itemFactory;

            public IncludeOperation(IncludeOperationBuilder builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
            {
                _elementOrder = builder.ElementOrder;
                _itemElement = builder.ItemElement;
                _rootDirectory = builder.RootDirectory;
                _itemType = builder.ItemType;
                _conditionResult = builder.ConditionResult;

                _operations = builder.Operations.ToImmutable();
                _referencedItemLists = builder.ReferencedItemLists.ToImmutable();
                _excludes = builder.Excludes.ToImmutable();
                if (builder.Metadata != null)
                {
                    _metadata = builder.Metadata.ToImmutable();
                }

                _lazyEvaluator = lazyEvaluator;

                _evaluatorData = new EvaluatorData(_lazyEvaluator._outerEvaluatorData, itemType => GetReferencedItems(itemType, ImmutableHashSet<string>.Empty));
                _expander = new Expander<P, I>(_evaluatorData, _evaluatorData);

                _itemFactory = new ItemFactoryWrapper(_itemElement, _lazyEvaluator._itemFactory);
            }

            public override void Apply(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                List<I> itemsToAdd = new List<I>();

                foreach (var operation in _operations)
                {
                    if (operation.Item1 == IncludeOperationType.Expression)
                    {
                        // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                        bool throwaway;
                        var itemsFromExpression = _expander.ExpandExpressionCaptureIntoItems(
                            (ExpressionShredder.ItemExpressionCapture) operation.Item2, _evaluatorData, _itemFactory, ExpanderOptions.ExpandItems,
                            false /* do not include null expansion results */, out throwaway, _itemElement.IncludeLocation);

                        itemsToAdd.AddRange(itemsFromExpression);
                    }
                    else if (operation.Item1 == IncludeOperationType.Value)
                    {
                        string value = (string)operation.Item2;
                        var item = _itemFactory.CreateItem(value, value, _itemElement.ContainingProject.FullPath);
                        itemsToAdd.Add(item);
                    }
                    else if (operation.Item1 == IncludeOperationType.Glob)
                    {
                        string glob = (string)operation.Item2;
                        string[] includeSplitFilesEscaped = EngineFileUtilities.GetFileListEscaped(_rootDirectory, glob);
                        foreach (string includeSplitFileEscaped in includeSplitFilesEscaped)
                        {
                            itemsToAdd.Add(_itemFactory.CreateItem(includeSplitFileEscaped, glob, _itemElement.ContainingProject.FullPath));
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(operation.Item1.ToString());
                    }
                }

                if (_excludes.Any())
                {
                    //  TODO: Pass exclusion list to globbing code so excluded files don't need to be scanned (twice)

                    HashSet<string> excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (string exclude in _excludes)
                    {
                        string excludeExpanded = _expander.ExpandIntoStringLeaveEscaped(exclude, ExpanderOptions.ExpandPropertiesAndItems, _itemElement.ExcludeLocation);
                        foreach (var excludeSplit in ExpressionShredder.SplitSemiColonSeparatedList(excludeExpanded))
                        {
                            string[] excludeSplitFiles = EngineFileUtilities.GetFileListEscaped(_rootDirectory, excludeSplit);

                            foreach (string excludeSplitFile in excludeSplitFiles)
                            {
                                excludes.Add(EscapingUtilities.UnescapeAll(excludeSplitFile));
                            }
                        }
                    }

                    List<I> remainingItems = new List<I>();

                    for (int i = 0; i < itemsToAdd.Count; i++)
                    {
                        if (!excludes.Contains(itemsToAdd[i].EvaluatedInclude))
                        {
                            remainingItems.Add(itemsToAdd[i]);
                        }
                    }

                    itemsToAdd = remainingItems;
                }

                if (_metadata != null)
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
                    List<string> values = new List<string>(_metadata.Count * 2);

                    foreach (ProjectMetadataElement metadatumElement in _metadata)
                    {
                        values.Add(metadatumElement.Value);
                        values.Add(metadatumElement.Condition);
                    }

                    ItemsAndMetadataPair itemsAndMetadataFound = ExpressionShredder.GetReferencedItemNamesAndMetadata(values);

                    bool needToProcessItemsIndividually = false;

                    if (itemsAndMetadataFound.Metadata != null && itemsAndMetadataFound.Metadata.Values.Count > 0)
                    {
                        // If there is bare metadata of any kind, and the Include involved an item list, we should
                        // run items individually, as even non-built-in metadata might differ between items

                        if (_referencedItemLists.Count >= 0)
                        {
                            needToProcessItemsIndividually = true;
                        }
                        else
                        {
                            // If there is bare built-in metadata, we must always run items individually, as that almost
                            // always differs between items.

                            // UNDONE: When batching is implemented for real, we need to make sure that
                            // item definition metadata is included in all metadata operations during evaluation
                            if (itemsAndMetadataFound.Metadata.Values.Count > 0)
                            {
                                needToProcessItemsIndividually = true;
                            }
                        }
                    }

                    if (needToProcessItemsIndividually)
                    {
                        foreach (I item in itemsToAdd)
                        {
                            _expander.Metadata = item;

                            foreach (ProjectMetadataElement metadatumElement in _metadata)
                            {
#if FEATURE_MSBUILD_DEBUGGER
                                //if (DebuggerManager.DebuggingEnabled)
                                //{
                                //    DebuggerManager.PulseState(metadatumElement.Location, _itemPassLocals);
                                //}
#endif

                                if (!EvaluateCondition(metadatumElement, ExpanderOptions.ExpandAll, ParserOptions.AllowAll, _expander, _lazyEvaluator))
                                {
                                    continue;
                                }

                                string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadatumElement.Value, ExpanderOptions.ExpandAll, metadatumElement.Location);

                                item.SetMetadata(metadatumElement, evaluatedValue);
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
                        EvaluatorMetadataTable metadataTable = new EvaluatorMetadataTable(_itemType);
                        _expander.Metadata = metadataTable;

                        // Also keep a list of everything so we can get the predecessor objects correct.
                        List<Pair<ProjectMetadataElement, string>> metadataList = new List<Pair<ProjectMetadataElement, string>>();

                        foreach (ProjectMetadataElement metadatumElement in _metadata)
                        {
                            // Because of the checking above, it should be safe to expand metadata in conditions; the condition
                            // will be true for either all the items or none
                            if (!EvaluateCondition(metadatumElement, ExpanderOptions.ExpandAll, ParserOptions.AllowAll, _expander, _lazyEvaluator))
                            {
                                continue;
                            }

#if FEATURE_MSBUILD_DEBUGGER
                        //if (DebuggerManager.DebuggingEnabled)
                        //{
                        //    DebuggerManager.PulseState(metadatumElement.Location, _itemPassLocals);
                        //}
#endif

                            string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadatumElement.Value, ExpanderOptions.ExpandAll, metadatumElement.Location);

                            metadataTable.SetValue(metadatumElement, evaluatedValue);
                            metadataList.Add(new Pair<ProjectMetadataElement, string>(metadatumElement, evaluatedValue));
                        }

                        // Apply those metadata to each item
                        // Note that several items could share the same metadata objects

                        // Set all the items at once to make a potential copy-on-write optimization possible.
                        // This is valuable in the case where one item element evaluates to
                        // many items (either by semicolon or wildcards)
                        // and that item also has the same piece/s of metadata for each item.
                        _itemFactory.SetMetadata(metadataList, itemsToAdd);

                        // End of legal area for metadata expressions.
                        _expander.Metadata = null;
                    }
                }

                listBuilder.AddRange(itemsToAdd.Select(item => new ItemData(item, _elementOrder, _conditionResult)));
            }

            IList<I> GetReferencedItems(string itemType, ImmutableHashSet<string> globsToIgnore)
            {
                LazyItemList itemList;
                if (_referencedItemLists.TryGetValue(itemType, out itemList))
                {
                    return itemList.GetItems(globsToIgnore)
                        .Where(ItemData => ItemData.ConditionResult)
                        .Select(itemData => itemData.Item)
                        .ToList();
                }
                else
                {
                    return ImmutableList<I>.Empty;
                }
            }
        }

        class IncludeOperationBuilder
        {
            public int ElementOrder { get; set; }
            public ProjectItemElement ItemElement { get; set; }
            public string RootDirectory { get; set; }
            public string ItemType { get; set; }
            public bool ConditionResult { get; set; }

            public ImmutableList<Tuple<IncludeOperationType, object>>.Builder Operations = ImmutableList.CreateBuilder<Tuple<IncludeOperationType, object>>();
            //public ImmutableList<string>.Builder Values { get; set; } = ImmutableList.CreateBuilder<string>();
            //public ImmutableList<string>.Builder Globs { get; set; } = ImmutableList.CreateBuilder<string>();
            public ImmutableDictionary<string, LazyItemList>.Builder ReferencedItemLists { get; set; } = ImmutableDictionary.CreateBuilder<string, LazyItemList>();
            //public ImmutableList<ExpressionShredder.ItemExpressionCapture>.Builder ItemExpressions { get; set; } = ImmutableList.CreateBuilder<ExpressionShredder.ItemExpressionCapture>();
            public ImmutableList<string>.Builder Excludes { get; set; } = ImmutableList.CreateBuilder<string>();
            public ImmutableList<ProjectMetadataElement>.Builder Metadata;

            public IncludeOperation CreateOperation(LazyItemEvaluator<P, I, M, D> lazyEvaluator)
            {
                return new IncludeOperation(this, lazyEvaluator);
            }
        }

        LazyItemList GetItemList(string itemType)
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
            if (itemElement.Include != null)
            {
                ProcessItemElementInclude(rootDirectory, itemElement, conditionResult);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        void ProcessItemElementInclude(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            IncludeOperationBuilder operationBuilder = new IncludeOperationBuilder();
            operationBuilder.ElementOrder = _nextElementOrder++;
            operationBuilder.ItemElement = itemElement;
            operationBuilder.RootDirectory = rootDirectory;
            operationBuilder.ItemType = itemElement.ItemType;
            operationBuilder.ConditionResult = conditionResult;

            //  Code corresponds to Evaluator.CreateItemsFromInclude

            // STEP 1: Expand properties in Include
            string evaluatedIncludeEscaped = _outerExpander.ExpandIntoStringLeaveEscaped(itemElement.Include, ExpanderOptions.ExpandProperties, itemElement.IncludeLocation);

            // STEP 2: Split Include on any semicolons, and take each split in turn
            if (evaluatedIncludeEscaped.Length > 0)
            {
                IList<string> includeSplitsEscaped = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedIncludeEscaped);

                foreach (string includeSplitEscaped in includeSplitsEscaped)
                {
                    // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                    bool isItemListExpression;
                    ProcessSingleItemVectorExpressionForInclude(includeSplitEscaped, operationBuilder, itemElement.IncludeLocation, out isItemListExpression);

                    if (!isItemListExpression)
                    {
                        // The expression is not of the form "@(X)". Treat as string

                        //  Code corresponds to EngineFileUtilities.GetFileList
                        bool containsEscapedWildcards = EscapingUtilities.ContainsEscapedWildcards(includeSplitEscaped);
                        bool containsRealWildcards = FileMatcher.HasWildcards(includeSplitEscaped);

                        if (containsEscapedWildcards && containsRealWildcards)
                        {
                            // Umm, this makes no sense.  The item's Include has both escaped wildcards and 
                            // real wildcards.  What does he want us to do?  Go to the file system and find
                            // files that literally have '*' in their filename?  Well, that's not going to 
                            // happen because '*' is an illegal character to have in a filename.

                            // Just return the original string.
                            operationBuilder.Operations.Add(Tuple.Create(IncludeOperationType.Value, (object) includeSplitEscaped));
                        }
                        else if (!containsEscapedWildcards && containsRealWildcards)
                        {
                            // Unescape before handing it to the filesystem.
                            string filespecUnescaped = EscapingUtilities.UnescapeAll(includeSplitEscaped);
                            operationBuilder.Operations.Add(Tuple.Create(IncludeOperationType.Glob, (object)filespecUnescaped));
                        }
                        else
                        {
                            // No real wildcards means we just return the original string.  Don't even bother 
                            // escaping ... it should already be escaped appropriately since it came directly
                            // from the project file
                            operationBuilder.Operations.Add(Tuple.Create(IncludeOperationType.Value, (object) includeSplitEscaped));
                        }

                    }
                }
            }

            //  Code corresponds to Evaluator.EvaluateItemElement

            // STEP 4: Evaluate, split, expand and subtract any Exclude
            if (itemElement.Exclude.Length > 0)
            {
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

            // STEP 5: Evaluate each metadata XML and apply them to each item we have so far
            if (itemElement.HasMetadata)
            {
                operationBuilder.Metadata = ImmutableList.CreateBuilder<ProjectMetadataElement>();
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

            var operation = operationBuilder.CreateOperation(this);

            LazyItemList previousItemList = GetItemList(itemElement.ItemType);
            LazyItemList newList = new LazyItemList(previousItemList, operation);
            _itemLists[itemElement.ItemType] = newList;
            
        }

        void AddItemReferences(string expression, IncludeOperationBuilder operationBuilder, IElementLocation elementLocation)
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

        void ProcessSingleItemVectorExpressionForInclude(string expression, IncludeOperationBuilder operationBuilder, IElementLocation elementLocation, out bool isItemListExpression)
        {
            isItemListExpression = false;

            //  Code corresponds to Expander.ExpandSingleItemVectorExpressionIntoItems
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

                isItemListExpression = true;

                operationBuilder.Operations.Add(Tuple.Create(IncludeOperationType.Expression, (object) match));

                AddReferencedItemLists(operationBuilder, match);
            }
        }

        void AddReferencedItemLists(IncludeOperationBuilder operationBuilder, ExpressionShredder.ItemExpressionCapture match)
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
