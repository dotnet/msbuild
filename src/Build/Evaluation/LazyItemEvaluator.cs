// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    /// <summary>
    ///  Provides the shared starting later Remove glob patterns using the filesystem's path-comparison policy.
    /// </summary>
    private static readonly ImmutableHashSet<string> s_emptyRemoveGlobPatterns =
        ImmutableHashSet.Create<string>(FileUtilities.PathComparer);

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

    private readonly Dictionary<string, ItemOperationSequence> _itemLists = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames
        ? []
        : [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    ///  Stores originating Include declarations and their saved condition results in collection order.
    /// </summary>
    private readonly SegmentedList<IncludeData> _includes = [];

    /// <summary>
    ///  Stores cached snapshots for this evaluation.
    /// </summary>
    private readonly Dictionary<ItemCacheKey, OrderedItemDataCollection> _itemCache = [];

    /// <summary>
    ///  Gets the cached snapshot count for deterministic lifetime tests.
    /// </summary>
    internal int TestOnlyCachedSnapshotCount => _itemCache.Count;

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
            ? sequence.GetCurrentFilteredItems()
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
            builder.Add(itemType, itemList);
        }
    }

    /// <summary>
    ///  Computes all final item snapshots and adds their items to the destination in declaration order.
    /// </summary>
    /// <param name="collector">The destination for items and design-time evaluation lists.</param>
    /// <remarks>
    ///  All snapshots are computed before any items are added. False-condition records are retained only
    ///  in the destination's condition-ignoring list when design-time evaluation is enabled.
    ///  <para>
    ///   Each item type's snapshot is already ordered by <see cref="ItemData.IncludeId"/>: Includes append
    ///   items in declaration order, Updates preserve their Include identity, and Removes preserve survivor
    ///   order. Walking the Include table supplies the global item-collection order; each declaration selects
    ///   its item type's next surviving run without sorting or comparing different streams.
    ///  </para>
    ///  <para>
    ///   One snapshot enumerator is retained per item type. A lookup maps item types to positioned
    ///   enumerators until they are exhausted, using the same name comparer as the operation lists.
    ///   Temporary storage is proportional to item-type count, not total item count.
    ///  </para>
    /// </remarks>
    public void AddAllItems(IItemEvaluationCollector<I> collector)
    {
        Assumed.NotNull(collector);

        if (_itemLists.Count == 0)
        {
            return;
        }

        ImmutablePieceList<ItemData>.Enumerator[] enumerators = new ImmutablePieceList<ItemData>.Enumerator[_itemLists.Count];
        Dictionary<string, int> itemTypeIndices = new(_itemLists.Count, _itemLists.Comparer);
        try
        {
            // Finish every final snapshot before creating snapshots or adding items anything to the collector. The enumerators
            // retain those snapshots; advancing them later cannot execute more item operations.
            int index = 0;
            foreach (var itemList in _itemLists)
            {
                enumerators[index] = itemList.Value.GetCurrentItemData().GetEnumerator();
                if (enumerators[index].MoveNext())
                {
                    itemTypeIndices.Add(itemList.Key, index);
                }

                index++;
            }

            bool recordDesignTimeItems = itemTypeIndices.Count > 0 && collector.ShouldEvaluateForDesignTime;
            for (int includeId = 0; includeId < _includes.Count && itemTypeIndices.Count > 0; includeId++)
            {
                IncludeData include = _includes[includeId];
                string itemType = include.ItemElement.ItemType;
                if (!itemTypeIndices.TryGetValue(itemType, out int itemTypeIndex))
                {
                    continue;
                }

                // Advance the actual struct enumerator, not a copy. If this Include produced no surviving
                // items, the enumerator is already at a later Include and the loop leaves it untouched.
                ref ImmutablePieceList<ItemData>.Enumerator enumerator = ref enumerators[itemTypeIndex];
                while (enumerator.Current.IncludeId == includeId)
                {
                    I item = enumerator.Current.Item;
                    if (include.ConditionResult)
                    {
                        collector.AddItem(item);

                        if (recordDesignTimeItems)
                        {
                            collector.AddToAllEvaluatedItemsList(item);
                        }
                    }

                    if (recordDesignTimeItems)
                    {
                        collector.AddItemIgnoringCondition(item);
                    }

                    if (!enumerator.MoveNext())
                    {
                        // Later declarations of this type have no surviving items either.
                        itemTypeIndices.Remove(itemType);
                        break;
                    }
                }
            }
        }
        finally
        {
            for (int i = 0; i < enumerators.Length; i++)
            {
                enumerators[i].Dispose();
            }
        }
    }

    /// <summary>
    ///  Gets the originating Include element and saved condition result for this evaluation.
    /// </summary>
    /// <param name="includeId">The Include identity carried by an item record.</param>
    /// <returns>
    ///  The data registered when the Include operation was created.
    /// </returns>
    private IncludeData GetIncludeData(int includeId) => _includes[includeId];

    private bool TryGetCachedItems(
        ItemOperation operation,
        ImmutableHashSet<string> laterRemoveGlobPatterns,
        [NotNullWhen(true)] out OrderedItemDataCollection items)
        => _itemCache.TryGetValue(new ItemCacheKey(operation, laterRemoveGlobPatterns), out items);

    private OrderedItemDataCollection GetCachedItems(ItemOperation operation, ImmutableHashSet<string> laterRemoveGlobPatterns)
        => _itemCache[new ItemCacheKey(operation, laterRemoveGlobPatterns)];

    private void CacheItems(ItemOperation operation, ImmutableHashSet<string> laterRemoveGlobPatterns, OrderedItemDataCollection items)
        => _itemCache[new ItemCacheKey(operation, laterRemoveGlobPatterns)] = items;

    private void RemoveCachedItems(ItemOperation operation, ImmutableHashSet<string> laterRemoveGlobPatterns)
        => _itemCache.Remove(new ItemCacheKey(operation, laterRemoveGlobPatterns));

    private void AddItemOperation(ProjectItemElement itemElement, bool conditionResult, string rootDirectory)
    {
        ItemOperation operation = CreateOperation(itemElement, conditionResult, rootDirectory);

        if (!_itemLists.TryGetValue(itemElement.ItemType, out ItemOperationSequence sequence))
        {
            sequence = new ItemOperationSequence(this);
            _itemLists.Add(itemElement.ItemType, sequence);
        }

        sequence.Add(operation);
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
        int includeId = _includes.Count;
        _includes.Add(new IncludeData(itemElement, conditionResult));
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
            includeId,
            rootDirectory,
            excludes?.ToImmutable() ?? ImmutableSegmentedList<string>.Empty,
            metadata);
    }

    private ItemOperation CreateRemoveOperation(ProjectItemElement itemElement, bool conditionResult, string rootDirectory)
    {
        using ItemListBookmarks.Builder builder = default;
        using RefArrayBuilder<string> matchOnMetadata = default;

        // Expand metadata names before choosing a parser. A present attribute can expand to nothing,
        // in which case Remove still accepts ordinary literal and glob specifications.
        if (itemElement.MatchOnMetadata.Length > 0)
        {
            // A property value can introduce item references that need bookmarks before expansion.
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
                        matchOnMetadata.Add(metadataSplit);
                    }
                }
            }
        }

        if (matchOnMetadata.IsEmpty)
        {
            // This includes a present MatchOnMetadata attribute that expands to nothing. Ordinary removal
            // matches identities against the specification, so only this path needs an ItemSpec.
            var itemSpec = ProcessItemSpec(itemElement.Remove, itemElement.RemoveLocation, rootDirectory, ref builder.AsRef());
            return new RemoveOperation(this, itemElement, itemSpec, conditionResult, builder.ToImmutable());
        }

        // Metadata matching accepts only item vectors. Reject literals and globs during parsing,
        // before resolving those vectors through the operation's bookmarks.
        var sourceFragments = ItemSpec.ParseItemVectorFragments(
            itemElement.Remove, _outerExpander, itemElement.RemoveLocation, rootDirectory);
        foreach (var fragment in sourceFragments)
        {
            AddItemListBookmark(fragment.ItemVector.ItemType, ref builder.AsRef());
        }

        var matchOnMetadataOptions = MatchOnMetadataOptions.CaseSensitive;
        if (Enum.TryParse(itemElement.MatchOnMetadataOptions, out MatchOnMetadataOptions options))
        {
            matchOnMetadataOptions = options;
        }

        // The constructor resolves vectors and stores metadata values now; applying the Remove is deferred.
        // Later source updates must not change the tuples this Remove matches.
        return new MetadataRemoveOperation(
            this,
            itemElement,
            sourceFragments,
            conditionResult,
            builder.ToImmutable(),
            matchOnMetadata.ToImmutable(),
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

    private ItemSpec ProcessItemSpec(
        string itemSpec,
        IElementLocation location,
        string rootDirectory,
        ref ItemListBookmarks.Builder builder)
        => new(itemSpec, ProcessItemFragments(itemSpec, location, rootDirectory, ref builder));

    private ImmutableArray<ItemSpecFragment> ProcessItemFragments(
        string itemSpec,
        IElementLocation location,
        string rootDirectory,
        ref ItemListBookmarks.Builder builder)
    {
        var fragments = ItemSpec.ParseFragments(itemSpec, _outerExpander, location, rootDirectory);

        foreach (ItemSpecFragment fragment in fragments)
        {
            if (fragment is ItemVectorFragment itemExpression)
            {
                // Captures holds the -> transform chain, not nested item vectors.
                // Only the top-level capture has an ItemType to reference.
                AddItemListBookmark(itemExpression.ItemVector.ItemType, ref builder);
            }
        }

        return fragments;
    }

    private ImmutableArray<ProjectMetadataElement> ProcessMetadataElements(ProjectItemElement itemElement, ref ItemListBookmarks.Builder builder)
    {
        using var metadataEnumerator = itemElement.GetMetadataEnumerator();
        if (!metadataEnumerator.MoveNext())
        {
            return [];
        }

        HashSet<string> referencedItemTypes = null;
        ProjectMetadataElement firstMetadata = metadataEnumerator.Current;
        CollectReferences(firstMetadata, ref referencedItemTypes);

        ImmutableArray<ProjectMetadataElement> metadata;

        if (!metadataEnumerator.MoveNext())
        {
            metadata = [firstMetadata];
        }
        else
        {
            using RefArrayBuilder<ProjectMetadataElement> metadataBuilder = default;
            metadataBuilder.Add(firstMetadata);

            do
            {
                ProjectMetadataElement metadataElement = metadataEnumerator.Current;
                metadataBuilder.Add(metadataElement);
                CollectReferences(metadataElement, ref referencedItemTypes);
            }
            while (metadataEnumerator.MoveNext());

            metadata = metadataBuilder.ToImmutable();
        }

        if (referencedItemTypes is not null)
        {
            foreach (string itemType in referencedItemTypes)
            {
                AddItemListBookmark(itemType, ref builder);
            }
        }

        return metadata;

        void CollectReferences(ProjectMetadataElement metadataElement, ref HashSet<string> itemTypes)
        {
            // Since we're just attempting to expand properties in order to find referenced items and not expanding metadata,
            // unexpected errors may occur when evaluating property functions on unexpanded metadata. Just ignore them if that happens.
            // See: https://github.com/dotnet/msbuild/issues/3460
            const ExpanderOptions expanderOptions = ExpanderOptions.ExpandProperties | ExpanderOptions.LeavePropertiesUnexpandedOnError;

            string expression = _expander.ExpandIntoStringLeaveEscaped(
                metadataElement.Value,
                expanderOptions,
                metadataElement.Location);

            ExpressionShredder.CollectReferencedItemVectorTypes(expression, ref itemTypes);

            expression = _expander.ExpandIntoStringLeaveEscaped(
                metadataElement.Condition,
                expanderOptions,
                metadataElement.ConditionLocation);

            ExpressionShredder.CollectReferencedItemVectorTypes(expression, ref itemTypes);
        }
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
