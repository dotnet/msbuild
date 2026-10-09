// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Evaluation;

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

    protected EvaluationContext EvaluationContext { get; }

    protected IFileSystem FileSystem => EvaluationContext.FileSystem;

    protected FileMatcher FileMatcher => EvaluationContext.FileMatcher;

    public LazyItemEvaluator(IEvaluatorData<P, I, M, D> data, IItemFactory<I, I> itemFactory, LoggingContext loggingContext, EvaluationProfiler evaluationProfiler, EvaluationContext evaluationContext)
    {
        _outerEvaluatorData = data;
        _outerExpander = new Expander<P, I>(_outerEvaluatorData, _outerEvaluatorData, evaluationContext, loggingContext);
        _evaluatorData = new EvaluatorData(_outerEvaluatorData, _itemLists);
        _expander = new Expander<P, I>(_evaluatorData, _evaluatorData, evaluationContext, loggingContext);
        _itemFactory = itemFactory;
        _loggingContext = loggingContext;
        _evaluationProfiler = evaluationProfiler;

        EvaluationContext = evaluationContext;
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
        LazyItemEvaluator<P, I, M, D> lazyEvaluator)
    {
        if (condition?.Length == 0)
        {
            return true;
        }
        MSBuildEventSource.Log.EvaluateConditionStart(condition);

        using (lazyEvaluator._evaluationProfiler.TrackCondition(element.ConditionLocation, condition))
        {
            bool result = ConditionEvaluator.EvaluateCondition(
                condition,
                parserOptions,
                expander,
                expanderOptions,
                GetCurrentDirectoryForConditionEvaluation(element, lazyEvaluator),
                element.ConditionLocation,
                lazyEvaluator.FileSystem,
                loggingContext: lazyEvaluator._loggingContext);
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
            Assumed.Unreachable();
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

        // Code corresponds to Evaluator.EvaluateItemElement

        // Process exclude (STEP 4: Evaluate, split, expand and subtract any Exclude)
        if (itemElement.Exclude.Length > 0)
        {
            // Expand properties here, because a property may have a value which is an item reference (ie "@(Bar)"), and
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

    private void ProcessMetadataElements(ProjectItemElement itemElement, OperationBuilderWithMetadata operationBuilder)
    {
        if (itemElement.HasMetadata)
        {
            ItemsAndMetadataPair itemsAndMetadataFound = new ItemsAndMetadataPair(null, null);

            // Since we're just attempting to expand properties in order to find referenced items and not expanding metadata,
            // unexpected errors may occur when evaluating property functions on unexpanded metadata. Just ignore them if that happens.
            // See: https://github.com/dotnet/msbuild/issues/3460
            const ExpanderOptions expanderOptions = ExpanderOptions.ExpandProperties | ExpanderOptions.LeavePropertiesUnexpandedOnError;
            foreach (var metadatumElement in itemElement.MetadataEnumerable)
            {
                operationBuilder.Metadata.Add(metadatumElement);

                string expression = _expander.ExpandIntoStringLeaveEscaped(
                    metadatumElement.Value,
                    expanderOptions,
                    metadatumElement.Location);

                ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);

                expression = _expander.ExpandIntoStringLeaveEscaped(
                    metadatumElement.Condition,
                    expanderOptions,
                    metadatumElement.ConditionLocation);

                ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);
            }

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
        if (Expander<P, I>.TryExpandSingleItemVectorExpression(
                expression,
                ExpanderOptions.ExpandItems,
                elementLocation,
                out ExpressionShredder.ItemExpressionCapture itemVector))
        {
            AddReferencedItemLists(operationBuilder, itemVector);
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
