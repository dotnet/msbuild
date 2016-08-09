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
                    var globsToIgnoreForPreviousOperations = globsToIgnore;

                    //  If this is a remove operation, then add any globs that will be removed
                    //  to the list of globs to ignore in previous operations
                    var removeOperation = _operation as RemoveOperation;
                    if (removeOperation != null)
                    {
                        var globsToIgnoreBuilder = removeOperation.GetRemovedGlobs();
                        foreach (var globToRemove in globsToIgnoreForPreviousOperations)
                        {
                            globsToIgnoreBuilder.Add(globToRemove);
                        }
                        globsToIgnoreForPreviousOperations = globsToIgnoreBuilder.ToImmutable();
                    }

                    items = _previous.GetItems(globsToIgnoreForPreviousOperations);
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

        // TODO: verify whether the ItemElement values are ever escaped
        enum ItemOperationType
        {
            //  Values are escaped
            Value,
            //  Globs are not escaped
            Glob,
            Expression
        }

        class OperationBuilder
        {
            public ProjectItemElement ItemElement { get; set; }
            public string ItemType { get; set; }
            public ImmutableList<Tuple<ItemOperationType, object>>.Builder Operations = ImmutableList.CreateBuilder<Tuple<ItemOperationType, object>>();
            public ImmutableDictionary<string, LazyItemList>.Builder ReferencedItemLists { get; set; } = ImmutableDictionary.CreateBuilder<string, LazyItemList>();
        }

        class OperationBuilderWithMetadata : OperationBuilder
        {
            public ImmutableList<ProjectMetadataElement>.Builder Metadata = ImmutableList.CreateBuilder<ProjectMetadataElement>();
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
            if (itemElement.Include != string.Empty)
            {
                ProcessItemElementInclude(rootDirectory, itemElement, conditionResult);
            }
            else if (itemElement.Remove != string.Empty)
            {
                ProcessItemElementRemove(rootDirectory, itemElement);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        void ProcessItemSpec(OperationBuilder operationBuilder, string itemSpec, ElementLocation itemSpecLocation)
        {
            //  Code corresponds to Evaluator.CreateItemsFromInclude

            // STEP 1: Expand properties in Include
            string evaluatedIncludeEscaped = _outerExpander.ExpandIntoStringLeaveEscaped(itemSpec, ExpanderOptions.ExpandProperties, itemSpecLocation);

            // STEP 2: Split Include on any semicolons, and take each split in turn
            if (evaluatedIncludeEscaped.Length > 0)
            {
                IList<string> includeSplitsEscaped = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedIncludeEscaped);

                foreach (string includeSplitEscaped in includeSplitsEscaped)
                {
                    // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                    bool isItemListExpression;
                    ProcessSingleItemVectorExpressionForInclude(includeSplitEscaped, operationBuilder, itemSpecLocation, out isItemListExpression);

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
                            operationBuilder.Operations.Add(Tuple.Create(ItemOperationType.Value, (object) includeSplitEscaped));
                        }
                        else if (!containsEscapedWildcards && containsRealWildcards)
                        {
                            // Unescape before handing it to the filesystem.
                            string filespecUnescaped = EscapingUtilities.UnescapeAll(includeSplitEscaped);
                            operationBuilder.Operations.Add(Tuple.Create(ItemOperationType.Glob, (object)filespecUnescaped));
                        }
                        else
                        {
                            // No real wildcards means we just return the original string.  Don't even bother 
                            // escaping ... it should already be escaped appropriately since it came directly
                            // from the project file
                            operationBuilder.Operations.Add(Tuple.Create(ItemOperationType.Value, (object) includeSplitEscaped));
                        }

                    }
                }
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

            // Process include
            ProcessItemSpec(operationBuilder, itemElement.Include, itemElement.IncludeLocation);

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

            var operation = operationBuilder.CreateOperation(this);

            LazyItemList previousItemList = GetItemList(itemElement.ItemType);
            LazyItemList newList = new LazyItemList(previousItemList, operation);
            _itemLists[itemElement.ItemType] = newList;

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

        void ProcessSingleItemVectorExpressionForInclude(string expression, OperationBuilder operationBuilder, IElementLocation elementLocation, out bool isItemListExpression)
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

                operationBuilder.Operations.Add(Tuple.Create(ItemOperationType.Expression, (object) match));

                AddReferencedItemLists(operationBuilder, match);
            }
        }

        void AddReferencedItemLists(OperationBuilder operationBuilder, ExpressionShredder.ItemExpressionCapture match)
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

        void ProcessItemElementRemove(string rootDirectory, ProjectItemElement itemElement)
        {
            OperationBuilder operationBuilder = new OperationBuilder();
            operationBuilder.ItemElement = itemElement;
            operationBuilder.ItemType = itemElement.ItemType;

            ProcessItemSpec(operationBuilder, itemElement.Remove, itemElement.RemoveLocation);

            var operation = new RemoveOperation(operationBuilder, this);

            LazyItemList previousItemList = GetItemList(itemElement.ItemType);
            LazyItemList newList = new LazyItemList(previousItemList, operation);
            _itemLists[itemElement.ItemType] = newList;
        }
    }
}
