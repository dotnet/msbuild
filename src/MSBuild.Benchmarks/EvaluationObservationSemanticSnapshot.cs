// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

internal enum EvaluationObservationSemanticDifference
{
    None,
    Imports,
    Properties,
    Items,
    Metadata,
}

internal readonly struct EvaluationObservationSemanticSummary
{
    internal EvaluationObservationSemanticSummary(
        int comparisonCount,
        int importCount,
        int propertyCount,
        int itemCount,
        int metadataCount)
    {
        ComparisonCount = comparisonCount;
        ImportCount = importCount;
        PropertyCount = propertyCount;
        ItemCount = itemCount;
        MetadataCount = metadataCount;
    }

    internal int ComparisonCount { get; }
    internal int ImportCount { get; }
    internal int PropertyCount { get; }
    internal int ItemCount { get; }
    internal int MetadataCount { get; }
}

internal sealed class EvaluationObservationSemanticSnapshot
{
    private static readonly NamedValueComparer s_namedValueComparer = new();
    private static readonly ItemGroupComparer s_itemGroupComparer = new();

    private readonly string[] _imports;
    private readonly NamedValue[] _properties;
    private readonly ItemGroup[] _itemGroups;

    private EvaluationObservationSemanticSnapshot(
        string[] imports,
        NamedValue[] properties,
        ItemGroup[] itemGroups)
    {
        _imports = imports;
        _properties = properties;
        _itemGroups = itemGroups;

        int itemCount = 0;
        int metadataCount = 0;
        for (int groupIndex = 0; groupIndex < itemGroups.Length; groupIndex++)
        {
            Item[] items = itemGroups[groupIndex].Items;
            itemCount += items.Length;
            for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
            {
                metadataCount += items[itemIndex].Metadata.Length;
            }
        }

        ItemCount = itemCount;
        MetadataCount = metadataCount;
    }

    internal int ImportCount => _imports.Length;
    internal int PropertyCount => _properties.Length;
    internal int ItemCount { get; }
    internal int MetadataCount { get; }

    internal static EvaluationObservationSemanticSnapshot Capture(ProjectInstance project)
    {
        IReadOnlyList<string> importPaths = project.ImportPathsIncludingDuplicates;
        string[] imports = new string[importPaths.Count];
        for (int i = 0; i < importPaths.Count; i++)
        {
            imports[i] = importPaths[i];
        }

        NamedValue[] properties = new NamedValue[project.Properties.Count];
        int propertyIndex = 0;
        foreach (ProjectPropertyInstance property in project.Properties)
        {
            properties[propertyIndex++] = new NamedValue(
                property.Name,
                ((IProperty)property).EvaluatedValueEscaped);
        }

        Array.Sort(properties, s_namedValueComparer);

        Dictionary<string, List<Item>> itemsByType = new(MSBuildNameIgnoreCaseComparer.Default);
        foreach (ProjectItemInstance item in project.Items)
        {
            if (!itemsByType.TryGetValue(item.ItemType, out List<Item>? items))
            {
                items = [];
                itemsByType.Add(item.ItemType, items);
            }

            List<NamedValue> metadata = [];
            foreach (ProjectMetadataInstance metadatum in item.Metadata)
            {
                metadata.Add(new NamedValue(metadatum.Name, metadatum.EvaluatedValueEscaped));
            }

            NamedValue[] sortedMetadata = [.. metadata];
            Array.Sort(sortedMetadata, s_namedValueComparer);
            items.Add(new Item(
                ((IItem)item).EvaluatedIncludeEscaped,
                sortedMetadata));
        }

        ItemGroup[] itemGroups = new ItemGroup[itemsByType.Count];
        int itemGroupIndex = 0;
        foreach (KeyValuePair<string, List<Item>> pair in itemsByType)
        {
            itemGroups[itemGroupIndex++] = new ItemGroup(pair.Key, [.. pair.Value]);
        }

        Array.Sort(itemGroups, s_itemGroupComparer);
        return new EvaluationObservationSemanticSnapshot(imports, properties, itemGroups);
    }

    internal void AssertEquivalent(EvaluationObservationSemanticSnapshot observed)
    {
        EvaluationObservationSemanticDifference difference = FindFirstDifference(observed, out string detail);
        if (difference != EvaluationObservationSemanticDifference.None)
        {
            throw new InvalidOperationException(
                $"Semantic evaluation mismatch in {difference.ToString().ToLowerInvariant()}: {detail}");
        }
    }

    internal EvaluationObservationSemanticSummary GetSummary(int comparisonCount = 0) =>
        new(comparisonCount, ImportCount, PropertyCount, ItemCount, MetadataCount);

    internal static void ValidateDifferenceDetection()
    {
        EvaluationObservationSemanticSnapshot baseline = CreateValidationSnapshot(
            ["first.props", "first.props", "second.props"],
            [new NamedValue("Property", "one")],
            [
                new ItemGroup(
                    "Input",
                    [
                        new Item("first", [new NamedValue("Metadata", "one")]),
                        new Item("second", [new NamedValue("Metadata", "two")]),
                        new Item("first", [new NamedValue("Metadata", "three")]),
                    ]),
                new ItemGroup("Other", [new Item("value", [])]),
            ]);

        baseline.AssertEquivalent(CreateValidationSnapshot(
            ["first.props", "first.props", "second.props"],
            [new NamedValue("Property", "one")],
            [
                new ItemGroup("Other", [new Item("value", [])]),
                new ItemGroup(
                    "Input",
                    [
                        new Item("first", [new NamedValue("Metadata", "one")]),
                        new Item("second", [new NamedValue("Metadata", "two")]),
                        new Item("first", [new NamedValue("Metadata", "three")]),
                    ]),
            ]));

        AssertDifference(
            baseline,
            CreateValidationSnapshot(
                ["first.props", "second.props", "first.props"],
                [new NamedValue("Property", "one")],
                [
                    new ItemGroup(
                        "Input",
                        [
                            new Item("first", [new NamedValue("Metadata", "one")]),
                            new Item("second", [new NamedValue("Metadata", "two")]),
                            new Item("first", [new NamedValue("Metadata", "three")]),
                        ]),
                    new ItemGroup("Other", [new Item("value", [])]),
                ]),
            EvaluationObservationSemanticDifference.Imports);

        AssertDifference(
            baseline,
            CreateValidationSnapshot(
                ["first.props", "first.props", "second.props"],
                [new NamedValue("Property", "two")],
                [
                    new ItemGroup(
                        "Input",
                        [
                            new Item("first", [new NamedValue("Metadata", "one")]),
                            new Item("second", [new NamedValue("Metadata", "two")]),
                            new Item("first", [new NamedValue("Metadata", "three")]),
                        ]),
                    new ItemGroup("Other", [new Item("value", [])]),
                ]),
            EvaluationObservationSemanticDifference.Properties);

        AssertDifference(
            baseline,
            CreateValidationSnapshot(
                ["first.props", "first.props", "second.props"],
                [new NamedValue("Property", "one")],
                [
                    new ItemGroup(
                        "Input",
                        [
                            new Item("second", [new NamedValue("Metadata", "one")]),
                            new Item("first", [new NamedValue("Metadata", "two")]),
                            new Item("first", [new NamedValue("Metadata", "three")]),
                        ]),
                    new ItemGroup("Other", [new Item("value", [])]),
                ]),
            EvaluationObservationSemanticDifference.Items);

        AssertDifference(
            baseline,
            CreateValidationSnapshot(
                ["first.props", "first.props", "second.props"],
                [new NamedValue("Property", "one")],
                [
                    new ItemGroup(
                        "Input",
                        [
                            new Item("first", [new NamedValue("Metadata", "changed")]),
                            new Item("second", [new NamedValue("Metadata", "two")]),
                            new Item("first", [new NamedValue("Metadata", "three")]),
                        ]),
                    new ItemGroup("Other", [new Item("value", [])]),
                ]),
            EvaluationObservationSemanticDifference.Metadata);
    }

    private EvaluationObservationSemanticDifference FindFirstDifference(
        EvaluationObservationSemanticSnapshot observed,
        out string detail)
    {
        if (_imports.Length != observed._imports.Length)
        {
            detail = $"reference count {_imports.Length}, observed count {observed._imports.Length}.";
            return EvaluationObservationSemanticDifference.Imports;
        }

        for (int i = 0; i < _imports.Length; i++)
        {
            if (!string.Equals(_imports[i], observed._imports[i], StringComparison.Ordinal))
            {
                detail =
                    $"entry {i} was '{Path.GetFileName(_imports[i])}' in the reference and " +
                    $"'{Path.GetFileName(observed._imports[i])}' when observed " +
                    $"({DescribeDifference(_imports[i], observed._imports[i])}).";
                return EvaluationObservationSemanticDifference.Imports;
            }
        }

        if (_properties.Length != observed._properties.Length)
        {
            detail = $"reference count {_properties.Length}, observed count {observed._properties.Length}.";
            return EvaluationObservationSemanticDifference.Properties;
        }

        for (int i = 0; i < _properties.Length; i++)
        {
            NamedValue referenceProperty = _properties[i];
            NamedValue observedProperty = observed._properties[i];
            if (!NamesEqual(referenceProperty.Name, observedProperty.Name))
            {
                detail =
                    $"entry {i} was '{referenceProperty.Name}' in the reference and '{observedProperty.Name}' when observed.";
                return EvaluationObservationSemanticDifference.Properties;
            }

            if (!string.Equals(referenceProperty.Value, observedProperty.Value, StringComparison.Ordinal))
            {
                detail = ValueDifference("property", referenceProperty.Name, referenceProperty.Value, observedProperty.Value);
                return EvaluationObservationSemanticDifference.Properties;
            }
        }

        if (_itemGroups.Length != observed._itemGroups.Length)
        {
            detail = $"reference type count {_itemGroups.Length}, observed type count {observed._itemGroups.Length}.";
            return EvaluationObservationSemanticDifference.Items;
        }

        for (int groupIndex = 0; groupIndex < _itemGroups.Length; groupIndex++)
        {
            ItemGroup referenceGroup = _itemGroups[groupIndex];
            ItemGroup observedGroup = observed._itemGroups[groupIndex];
            if (!NamesEqual(referenceGroup.ItemType, observedGroup.ItemType))
            {
                detail =
                    $"type entry {groupIndex} was '{referenceGroup.ItemType}' in the reference " +
                    $"and '{observedGroup.ItemType}' when observed.";
                return EvaluationObservationSemanticDifference.Items;
            }

            if (referenceGroup.Items.Length != observedGroup.Items.Length)
            {
                detail =
                    $"type '{referenceGroup.ItemType}' had {referenceGroup.Items.Length} items in the reference " +
                    $"and {observedGroup.Items.Length} when observed.";
                return EvaluationObservationSemanticDifference.Items;
            }

            for (int itemIndex = 0; itemIndex < referenceGroup.Items.Length; itemIndex++)
            {
                Item referenceItem = referenceGroup.Items[itemIndex];
                Item observedItem = observedGroup.Items[itemIndex];
                if (!string.Equals(
                    referenceItem.EvaluatedIncludeEscaped,
                    observedItem.EvaluatedIncludeEscaped,
                    StringComparison.Ordinal))
                {
                    detail =
                        $"item {itemIndex} of type '{referenceGroup.ItemType}' had a different include " +
                        $"({DescribeDifference(referenceItem.EvaluatedIncludeEscaped, observedItem.EvaluatedIncludeEscaped)}).";
                    return EvaluationObservationSemanticDifference.Items;
                }

                if (referenceItem.Metadata.Length != observedItem.Metadata.Length)
                {
                    detail =
                        $"item {itemIndex} of type '{referenceGroup.ItemType}' had {referenceItem.Metadata.Length} " +
                        $"metadata entries in the reference and {observedItem.Metadata.Length} when observed.";
                    return EvaluationObservationSemanticDifference.Metadata;
                }

                for (int metadataIndex = 0; metadataIndex < referenceItem.Metadata.Length; metadataIndex++)
                {
                    NamedValue referenceMetadata = referenceItem.Metadata[metadataIndex];
                    NamedValue observedMetadata = observedItem.Metadata[metadataIndex];
                    if (!NamesEqual(referenceMetadata.Name, observedMetadata.Name))
                    {
                        detail =
                            $"item {itemIndex} of type '{referenceGroup.ItemType}' metadata entry {metadataIndex} " +
                            $"was '{referenceMetadata.Name}' in the reference and '{observedMetadata.Name}' when observed.";
                        return EvaluationObservationSemanticDifference.Metadata;
                    }

                    if (!string.Equals(referenceMetadata.Value, observedMetadata.Value, StringComparison.Ordinal))
                    {
                        detail = ValueDifference(
                            $"item {itemIndex} of type '{referenceGroup.ItemType}' metadata",
                            referenceMetadata.Name,
                            referenceMetadata.Value,
                            observedMetadata.Value);
                        return EvaluationObservationSemanticDifference.Metadata;
                    }
                }
            }
        }

        detail = string.Empty;
        return EvaluationObservationSemanticDifference.None;
    }

    private static bool NamesEqual(string left, string right) =>
        MSBuildNameIgnoreCaseComparer.Default.Equals(left, right);

    private static string ValueDifference(string kind, string name, string reference, string observed) =>
        $"{kind} '{name}' had a different value " +
        $"({DescribeDifference(reference, observed)}).";

    private static string DescribeDifference(string reference, string observed)
    {
        int commonLength = Math.Min(reference.Length, observed.Length);
        int differenceIndex = 0;
        while (differenceIndex < commonLength &&
            reference[differenceIndex] == observed[differenceIndex])
        {
            differenceIndex++;
        }

        return $"reference length {reference.Length}, observed length {observed.Length}, " +
            $"first difference at character {differenceIndex}";
    }

    private static EvaluationObservationSemanticSnapshot CreateValidationSnapshot(
        string[] imports,
        NamedValue[] properties,
        ItemGroup[] itemGroups)
    {
        Array.Sort(properties, s_namedValueComparer);
        for (int groupIndex = 0; groupIndex < itemGroups.Length; groupIndex++)
        {
            Item[] items = itemGroups[groupIndex].Items;
            for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
            {
                Array.Sort(items[itemIndex].Metadata, s_namedValueComparer);
            }
        }

        Array.Sort(itemGroups, s_itemGroupComparer);
        return new EvaluationObservationSemanticSnapshot(imports, properties, itemGroups);
    }

    private static void AssertDifference(
        EvaluationObservationSemanticSnapshot reference,
        EvaluationObservationSemanticSnapshot observed,
        EvaluationObservationSemanticDifference expected)
    {
        EvaluationObservationSemanticDifference actual = reference.FindFirstDifference(observed, out _);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Semantic comparison self-validation expected {expected} but detected {actual}.");
        }
    }

    private sealed class NamedValue
    {
        internal NamedValue(string name, string value)
        {
            Name = name;
            Value = value;
        }

        internal string Name { get; }
        internal string Value { get; }
    }

    private sealed class Item
    {
        internal Item(string evaluatedIncludeEscaped, NamedValue[] metadata)
        {
            EvaluatedIncludeEscaped = evaluatedIncludeEscaped;
            Metadata = metadata;
        }

        internal string EvaluatedIncludeEscaped { get; }
        internal NamedValue[] Metadata { get; }
    }

    private sealed class ItemGroup
    {
        internal ItemGroup(string itemType, Item[] items)
        {
            ItemType = itemType;
            Items = items;
        }

        internal string ItemType { get; }
        internal Item[] Items { get; }
    }

    private sealed class NamedValueComparer : IComparer<NamedValue>
    {
        public int Compare(NamedValue? left, NamedValue? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            int result = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return result != 0
                ? result
                : StringComparer.Ordinal.Compare(left.Name, right.Name);
        }
    }

    private sealed class ItemGroupComparer : IComparer<ItemGroup>
    {
        public int Compare(ItemGroup? left, ItemGroup? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            int result = StringComparer.OrdinalIgnoreCase.Compare(left.ItemType, right.ItemType);
            return result != 0
                ? result
                : StringComparer.Ordinal.Compare(left.ItemType, right.ItemType);
        }
    }
}
