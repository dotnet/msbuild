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
using Microsoft.CodeAnalysis.Collections;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D> : IItemProvider<I>
    where P : class, IProperty, IEquatable<P>, IValued
    where I : class, IItem<M>, IMetadataTable
    where M : class, IMetadatum
    where D : class, IItemDefinition<M>
{
    private readonly IEvaluatorData<P, I, M, D> _data;
    private readonly Expander<P, I> _outerExpander;
    private readonly Expander<P, I> _expander;
    private readonly IItemFactory<I, I> _itemFactory;
    private readonly LoggingContext _loggingContext;
    private readonly EvaluationProfiler _evaluationProfiler;

    /// <summary>
    ///  Records condition-true item elements for optional evaluated-element tracking
    ///  and item-glob synthesis.
    /// </summary>
    private readonly EvaluatedItemElementRecorder _evaluatedItemElementRecorder;

    private int _nextElementOrder = 0;

    private readonly Dictionary<string, ItemOperationSequence> _itemLists = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames
        ? []
        : [with(StringComparer.OrdinalIgnoreCase)];

    protected EvaluationContext EvaluationContext { get; }

    protected IFileSystem FileSystem => EvaluationContext.FileSystem;

    protected FileMatcher FileMatcher => EvaluationContext.FileMatcher;

    public LazyItemEvaluator(
        IEvaluatorData<P, I, M, D> data,
        IItemFactory<I, I> itemFactory,
        LoggingContext loggingContext,
        EvaluationProfiler evaluationProfiler,
        EvaluationContext evaluationContext,
        EvaluatedItemElementRecorder evaluatedItemElementRecorder)
    {
        _data = data;
        _outerExpander = new Expander<P, I>(_data, _data, evaluationContext, loggingContext);
        _expander = new Expander<P, I>(_data, this, evaluationContext, loggingContext);
        _itemFactory = itemFactory;
        _loggingContext = loggingContext;
        _evaluationProfiler = evaluationProfiler;
        _evaluatedItemElementRecorder = evaluatedItemElementRecorder;

        EvaluationContext = evaluationContext;
    }

    /// <inheritdoc />
    ICollection<I> IItemProvider<I>.GetItems(string itemType)
        => _itemLists.TryGetValue(itemType, out ItemOperationSequence sequence)
            ? sequence.BookmarkCurrent().GetFilteredItems()
            : Array.Empty<I>();

    /// <summary>
    ///  Processes item groups in order and collects their applicable item operations.
    /// </summary>
    /// <param name="itemGroupElements">The item groups in evaluation order.</param>
    /// <param name="rootDirectory">The root project's directory, including for imported item elements.</param>
    public void ProcessItemGroups(List<ProjectItemGroupElement> itemGroupElements, string rootDirectory)
    {
        foreach (ProjectItemGroupElement itemGroup in itemGroupElements)
        {
            using (_evaluationProfiler.TrackElement(itemGroup))
            {
                ProcessItemGroupElement(itemGroup, rootDirectory);
            }
        }
    }

    /// <summary>
    ///  Processes the items in an item group and collects the applicable item operations.
    /// </summary>
    /// <param name="itemGroupElement">The item group to process.</param>
    /// <param name="rootDirectory">The root project's directory, including for imported item elements.</param>
    private void ProcessItemGroupElement(ProjectItemGroupElement itemGroupElement, string rootDirectory)
    {
        bool groupCondition = EvaluateCondition(
            itemGroupElement,
            ExpanderOptions.ExpandPropertiesAndItems,
            ParserOptions.AllowPropertiesAndItemLists);

        bool keepEvaluating = _data.ShouldEvaluateForDesignTime && _data.CanEvaluateElementsWithFalseConditions;

        if (groupCondition || keepEvaluating)
        {
            foreach (ProjectItemElement itemElement in itemGroupElement.Items)
            {
                using (_evaluationProfiler.TrackElement(itemElement))
                {
                    bool itemCondition = EvaluateCondition(
                        itemElement,
                        ExpanderOptions.ExpandPropertiesAndItems,
                        ParserOptions.AllowPropertiesAndItemLists);

                    if (itemCondition || keepEvaluating)
                    {
                        bool conditionResult = groupCondition && itemCondition;

                        AddItemOperation(itemElement, conditionResult, rootDirectory);

                        if (conditionResult)
                        {
                            _evaluatedItemElementRecorder.Record(itemElement);
                        }
                    }
                }
            }
        }
    }

    private bool EvaluateCondition(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        => EvaluateCondition(element, _expander, expanderOptions, parserOptions);

    private bool EvaluateCondition(ProjectElement element, Expander<P, I> expander, ExpanderOptions expanderOptions, ParserOptions parserOptions)
    {
        string condition = element.Condition;
        if (condition?.Length == 0)
        {
            return true;
        }

        MSBuildEventSource.Log.EvaluateConditionStart(condition);

        using (_evaluationProfiler.TrackCondition(element.ConditionLocation, condition))
        {
            bool result = ConditionEvaluator.EvaluateCondition(
                condition,
                parserOptions,
                expander,
                expanderOptions,
                GetCurrentDirectoryForConditionEvaluation(element),
                element.ConditionLocation,
                FileSystem,
                loggingContext: _loggingContext);

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
    private string GetCurrentDirectoryForConditionEvaluation(ProjectElement element)
        => element is ProjectPropertyGroupElement or ProjectImportElement or ProjectImportGroupElement
            ? element.ContainingProject.DirectoryPath
            : _data.Directory;

    private void AddItemListBookmark(string itemType, ref ItemListBookmarks.Builder builder)
    {
        if (_itemLists.TryGetValue(itemType, out ItemOperationSequence sequence))
        {
            ItemListBookmark itemList = sequence.BookmarkCurrent();
            itemList.MarkAsNeeded();
            builder.Add(itemType, itemList);
        }
    }

    /// <summary>
    ///  Computes the final items and adds them to the collector in declaration order.
    /// </summary>
    /// <param name="collector">The destination for items and design-time evaluation lists.</param>
    public void AddAllItems(IItemEvaluationCollector<I> collector)
    {
        ArgumentNullException.ThrowIfNull(collector);

        foreach (ItemData itemData in GetAllItemsDeferred())
        {
            if (itemData.ConditionResult)
            {
                collector.AddItem(itemData.Item);

                if (collector.ShouldEvaluateForDesignTime)
                {
                    collector.AddToAllEvaluatedItemsList(itemData.Item);
                }
            }

            if (collector.ShouldEvaluateForDesignTime)
            {
                collector.AddItemIgnoringCondition(itemData.Item);
            }
        }
    }

    private IEnumerable<ItemData> GetAllItemsDeferred()
    {
        return _itemLists.Values.SelectMany(sequence => sequence.BookmarkCurrent().GetItemData())
                                .OrderBy(itemData => itemData.ElementOrder);
    }

    private void AddItemOperation(ProjectItemElement itemElement, bool conditionResult, string rootDirectory)
    {
        ItemOperation operation = CreateOperation(itemElement, conditionResult, rootDirectory);

        if (!_itemLists.TryGetValue(itemElement.ItemType, out ItemOperationSequence sequence))
        {
            sequence = new ItemOperationSequence();
        }

        sequence.Add(operation);
        _itemLists[itemElement.ItemType] = sequence;
    }

    private ItemOperation CreateOperation(ProjectItemElement itemElement, bool conditionResult, string rootDirectory)
        => itemElement.IncludeLocation != null
            ? CreateIncludeOperation(itemElement, conditionResult, rootDirectory)
         : itemElement.RemoveLocation != null
            ? CreateRemoveOperation(itemElement, conditionResult, rootDirectory)
         : itemElement.UpdateLocation != null
            ? CreateUpdateOperation(itemElement, conditionResult, rootDirectory)
         : Assumed.Unreachable<ItemOperation>();

    private IncludeOperation CreateIncludeOperation(ProjectItemElement itemElement, bool conditionResult, string rootDirectory)
    {
        int elementOrder = _nextElementOrder++;
        using ItemListBookmarks.Builder builder = default;

        // Process include
        var itemSpec = ProcessItemSpec(itemElement.Include, itemElement.IncludeLocation, rootDirectory, ref builder.AsRef());

        ImmutableSegmentedList<string>.Builder excludes = null;

        // Process exclude (STEP 4: Evaluate, split, expand and subtract any Exclude)
        if (itemElement.Exclude.Length > 0)
        {
            // Expand properties here, because a property may have a value which is an item reference (ie "@(Bar)"), and
            //  if so we need to add the right item reference
            string evaluatedExclude = _expander.ExpandIntoStringLeaveEscaped(
                itemElement.Exclude,
                ExpanderOptions.ExpandProperties,
                itemElement.ExcludeLocation);

            if (evaluatedExclude.Length > 0)
            {
                foreach (string excludeSplit in ExpressionShredder.SplitSemiColonSeparatedList(evaluatedExclude))
                {
                    excludes ??= ImmutableSegmentedList.CreateBuilder<string>();
                    excludes.Add(excludeSplit);

                    AddItemReferences(excludeSplit, itemElement.ExcludeLocation, ref builder.AsRef());
                }
            }
        }

        // Process Metadata (STEP 5: Evaluate each metadata XML and apply them to each item we have so far)
        var metadata = ProcessMetadataElements(itemElement, ref builder.AsRef());

        return new(
            this,
            itemElement,
            itemSpec,
            conditionResult,
            builder.ToImmutable(),
            elementOrder,
            rootDirectory,
            excludes?.ToImmutable() ?? ImmutableSegmentedList<string>.Empty,
            metadata);
    }

    private RemoveOperation CreateRemoveOperation(ProjectItemElement itemElement, bool conditionResult, string rootDirectory)
    {
        using ItemListBookmarks.Builder builder = default;
        var itemSpec = ProcessItemSpec(itemElement.Remove, itemElement.RemoveLocation, rootDirectory, ref builder.AsRef());

        ImmutableList<string>.Builder matchOnMetadata = null;

        // Process MatchOnMetadata
        if (itemElement.MatchOnMetadata.Length > 0)
        {
            string evaluatedmatchOnMetadata = _expander.ExpandIntoStringLeaveEscaped(
                itemElement.MatchOnMetadata,
                ExpanderOptions.ExpandProperties,
                itemElement.MatchOnMetadataLocation);

            if (evaluatedmatchOnMetadata.Length > 0)
            {
                foreach (string matchOnMetadataSplit in ExpressionShredder.SplitSemiColonSeparatedList(evaluatedmatchOnMetadata))
                {
                    AddItemReferences(matchOnMetadataSplit, itemElement.MatchOnMetadataLocation, ref builder.AsRef());

                    string metadataExpanded = _expander.ExpandIntoStringLeaveEscaped(
                        matchOnMetadataSplit,
                        ExpanderOptions.ExpandPropertiesAndItems,
                        itemElement.MatchOnMetadataLocation);

                    foreach (string metadataSplit in ExpressionShredder.SplitSemiColonSeparatedList(metadataExpanded))
                    {
                        matchOnMetadata ??= ImmutableList.CreateBuilder<string>();
                        matchOnMetadata.Add(metadataSplit);
                    }
                }
            }
        }

        var matchOnMetadataOptions = MatchOnMetadataOptions.CaseSensitive;
        if (Enum.TryParse(itemElement.MatchOnMetadataOptions, out MatchOnMetadataOptions options))
        {
            matchOnMetadataOptions = options;
        }

        return new(
            this,
            itemElement,
            itemSpec,
            conditionResult,
            builder.ToImmutable(),
            matchOnMetadata?.ToImmutable() ?? [],
            matchOnMetadataOptions);
    }

    private UpdateOperation CreateUpdateOperation(ProjectItemElement itemElement, bool conditionResult, string rootDirectory)
    {
        using ItemListBookmarks.Builder builder = default;
        var itemSpec = ProcessItemSpec(itemElement.Update, itemElement.UpdateLocation, rootDirectory, ref builder.AsRef());
        var metadata = ProcessMetadataElements(itemElement, ref builder.AsRef());

        return new(
            this,
            itemElement,
            itemSpec,
            conditionResult,
            builder.ToImmutable(),
            metadata);
    }

    private ItemSpec<P, I> ProcessItemSpec(
        string itemSpec,
        IElementLocation location,
        string rootDirectory,
        ref ItemListBookmarks.Builder builder)
    {
        ItemSpec<P, I> result = new(itemSpec, _outerExpander, location, rootDirectory);

        foreach (ItemSpecFragment fragment in result.Fragments)
        {
            if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemExpression)
            {
                // Captures holds the -> transform chain, not nested item vectors.
                // Only the top-level capture has an ItemType to reference.
                AddItemListBookmark(itemExpression.Capture.ItemType, ref builder);
            }
        }

        return result;
    }

    private ImmutableArray<ProjectMetadataElement> ProcessMetadataElements(ProjectItemElement itemElement, ref ItemListBookmarks.Builder builder)
    {
        if (!itemElement.HasMetadata)
        {
            return [];
        }

        ItemsAndMetadataPair itemsAndMetadataFound = default;

        // Since we're just attempting to expand properties in order to find referenced items and not expanding metadata,
        // unexpected errors may occur when evaluating property functions on unexpanded metadata. Just ignore them if that happens.
        // See: https://github.com/dotnet/msbuild/issues/3460
        const ExpanderOptions expanderOptions = ExpanderOptions.ExpandProperties | ExpanderOptions.LeavePropertiesUnexpandedOnError;

        using RefArrayBuilder<ProjectMetadataElement> metadata = default;

        foreach (var metadataElement in itemElement.MetadataEnumerable)
        {
            metadata.Add(metadataElement);

            string expression = _expander.ExpandIntoStringLeaveEscaped(
                metadataElement.Value,
                expanderOptions,
                metadataElement.Location);

            ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);

            expression = _expander.ExpandIntoStringLeaveEscaped(
                metadataElement.Condition,
                expanderOptions,
                metadataElement.ConditionLocation);

            ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);
        }

        if (itemsAndMetadataFound.Items != null)
        {
            foreach (var itemType in itemsAndMetadataFound.Items)
            {
                AddItemListBookmark(itemType, ref builder);
            }
        }

        return metadata.ToImmutable();
    }

    private void AddItemReferences(string expression, IElementLocation location, ref ItemListBookmarks.Builder builder)
    {
        if (Expander<P, I>.TryExpandSingleItemVectorExpression(
                expression,
                ExpanderOptions.ExpandItems,
                location,
                out ExpressionShredder.ItemExpressionCapture itemVector))
        {
            // Captures holds the -> transform chain, not nested item vectors.
            // Only the top-level capture has an ItemType to reference.
            AddItemListBookmark(itemVector.ItemType, ref builder);
        }
    }
}
