// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build;

namespace MSBuild.Benchmarks;

/// <summary>
///  Generates deterministic in-memory declarations and models their expected results outside benchmark timing.
/// </summary>
internal sealed class LazyItemBenchmarkScenario
{
    private const int OperationCount = 32;
    private const string InitialTrace = "r";
    private readonly StringBuilder _xml = new("<Project>");
    private readonly List<(string Include, string Trace, int Order)> _source = [];
    private readonly List<(string ItemType, string Include, string Trace, int Order)> _otherItems = [];
    private int _nextOrder;

    private LazyItemBenchmarkScenario()
    {
    }

    public string ProjectXml { get; private set; } = null!;

    public int SourceCount => _source.Count;

    public List<string> Files { get; } = [];

    public List<(string ItemType, string Include, string Trace)> ExpectedItems { get; } = [];

    public static LazyItemBenchmarkScenario CreateOperations(LazyItemOperationBenchmark.WorkloadKind workload, int itemCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, OperationCount * 10);

        LazyItemBenchmarkScenario scenario = new();

        if (workload == LazyItemOperationBenchmark.WorkloadKind.MetadataRemove)
        {
            scenario.AppendItem("RemoveKey", "key", "value0");
            for (int item = 0; item < itemCount; item++)
            {
                scenario.AppendItem("Source", ItemName(item), $"value{item % 4}");
            }

            scenario._xml.Append(
                """<ItemGroup><Source Remove="@(RemoveKey)" MatchOnMetadata="Trace" /></ItemGroup>""");

            scenario._source.RemoveAll(static item => item.Trace == "value0");
            return scenario.Complete();
        }

        bool duplicates = workload is LazyItemOperationBenchmark.WorkloadKind.IndexedDuplicatePathRemoves
                                   or LazyItemOperationBenchmark.WorkloadKind.BulkDuplicatePathRemoves;
        bool individualIncludes = workload is LazyItemOperationBenchmark.WorkloadKind.IndividualIncludes;
        scenario.AppendItems(0, itemCount, duplicates, individualIncludes);

        switch (workload)
        {
            case LazyItemOperationBenchmark.WorkloadKind.MetadataWildcardUpdate:
                scenario._xml.Append("""<ItemGroup><Source Update="*"><Trace>%(Filename)</Trace></Source></ItemGroup>""");
                for (int index = 0; index < scenario._source.Count; index++)
                {
                    var item = scenario._source[index];
                    scenario._source[index] = (item.Include, Path.GetFileNameWithoutExtension(item.Include), item.Order);
                }

                return scenario.Complete();

            case LazyItemOperationBenchmark.WorkloadKind.BulkLiteralRemoves:
            case LazyItemOperationBenchmark.WorkloadKind.BulkDuplicatePathRemoves:
                HashSet<string> removed = [with(StringComparer.Ordinal)];
                for (int item = 0; item < itemCount; item += 2)
                {
                    removed.Add(ItemName(item));
                }

                scenario.AppendRemove(string.Join(";", removed), include => removed.Contains(include.StartsWith("./", StringComparison.Ordinal)
                    ? include.Substring(2)
                    : include));

                return scenario.Complete();
        }

        for (int operation = 0; operation < OperationCount; operation++)
        {
            switch (workload)
            {
                case LazyItemOperationBenchmark.WorkloadKind.IncludeOnly:
                case LazyItemOperationBenchmark.WorkloadKind.IndividualIncludes:
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.DisjointLiteralUpdates:
                    scenario.AppendUpdate(ItemName(operation), index => index == operation);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.OverlappingLiteralUpdates:
                    scenario.AppendUpdate(ItemName(0), static index => index == 0);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.SparseWildcardUpdates:
                    scenario.AppendUpdate("*.cs", static index => index % 10 == 0);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.DenseWildcardUpdates:
                    scenario.AppendUpdate("*", static _ => true);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.IndexedLiteralRemoves:
                    string literal = ItemName(operation);
                    scenario.AppendRemove(literal, item => item == literal);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.IndexedWildcardRemoves:
                    string prefix = $"item{operation:D5}";
                    scenario.AppendRemove($"{prefix}?.*", item => item.StartsWith(prefix, StringComparison.Ordinal));
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.IndexedDuplicatePathRemoves:
                    string path = ItemName(operation);
                    scenario.AppendRemove($"{path};./{path};{path}", item => item == path || item == $"./{path}");
                    break;

                default:
                    Assumed.Unreachable($"Unexpected workload: {workload}");
                    break;
            }
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateSequence(LazyItemSequenceBenchmark.WorkloadKind workload, int operationCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(operationCount, 1);

        LazyItemBenchmarkScenario scenario = new();

        switch (workload)
        {
            case LazyItemSequenceBenchmark.WorkloadKind.DisjointLiteralUpdates:
            case LazyItemSequenceBenchmark.WorkloadKind.OverlappingLiteralUpdates:
                {
                    scenario.AppendItems(0, operationCount);
                    for (int operation = 0; operation < operationCount; operation++)
                    {
                        int target = workload is LazyItemSequenceBenchmark.WorkloadKind.DisjointLiteralUpdates
                            ? operation
                            : operation % Math.Min(8, operationCount);

                        scenario.AppendUpdate(ItemName(target), index => index == target);
                    }

                    break;
                }

            case LazyItemSequenceBenchmark.WorkloadKind.IndividualIncludes:
            case LazyItemSequenceBenchmark.WorkloadKind.InterleavedItemTypes:
                {
                    for (int operation = 0; operation < operationCount; operation++)
                    {
                        string itemType = workload is LazyItemSequenceBenchmark.WorkloadKind.InterleavedItemTypes
                            ? $"Type{operation % 32}"
                            : "Source";

                        scenario.AppendItem(itemType, ItemName(operation), InitialTrace);
                    }

                    break;
                }

            default:
                Assumed.Unreachable($"Unexpected workload: {workload}");
                break;
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateCurrentReads(LazyItemCurrentReadBenchmark.WorkloadKind workload, int itemCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, 1);

        LazyItemBenchmarkScenario scenario = new();

        switch (workload)
        {
            case LazyItemCurrentReadBenchmark.WorkloadKind.SparseGrowingReads:
            case LazyItemCurrentReadBenchmark.WorkloadKind.EveryGrowingRead:
                for (int item = 0; item < itemCount; item++)
                {
                    scenario.AppendItem("Source", ItemName(item), InitialTrace);
                    if (workload == LazyItemCurrentReadBenchmark.WorkloadKind.EveryGrowingRead || (item + 1) % 100 == 0)
                    {
                        scenario.AppendReadCount();
                    }
                }

                break;

            case LazyItemCurrentReadBenchmark.WorkloadKind.ReadAfterEveryUpdate:
                scenario.AppendItems(0, itemCount);
                for (int item = 0; item < itemCount; item++)
                {
                    scenario.AppendUpdate(ItemName(item), index => index == item);
                    scenario.AppendReadCount();
                }

                break;

            case LazyItemCurrentReadBenchmark.WorkloadKind.RepeatedCurrentReads:
                scenario.AppendItems(0, itemCount);
                for (int read = 0; read < 100; read++)
                {
                    scenario.AppendReadCount();
                }

                break;

            default:
                Assumed.Unreachable($"Unexpected workload: {workload}");
                break;
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateQualifiedUpdate(int sourceCount, int targetCount, bool wildcardSources = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetCount, 1);

        LazyItemBenchmarkScenario scenario = new();

        for (int source = 0; source < sourceCount; source++)
        {
            scenario.AppendItem("Lookup", ItemName(source), $"lookup{source}");
        }

        scenario.AppendItems(0, targetCount);
        string update = wildcardSources ? "@(Lookup->'%(Identity)*')" : "@(Lookup)";
        scenario._xml.Append($"""<ItemGroup><Source Update="{update}"><Trace>%(Lookup.Trace)</Trace></Source></ItemGroup>""");

        for (int target = 0; target < Math.Min(sourceCount, targetCount); target++)
        {
            (string? include, string? trace, int order) = scenario._source[target];
            scenario._source[target] = (include, $"lookup{target}", order);
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateExcludes(int itemCount, int excludeCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(excludeCount);

        LazyItemBenchmarkScenario scenario = new();

        string[] includes = new string[itemCount];
        for (int item = 0; item < itemCount; item++)
        {
            includes[item] = ItemName(item);
            scenario._source.Add((includes[item], InitialTrace, scenario._nextOrder++));
        }

        string[] excludes = new string[excludeCount];
        for (int exclude = 0; exclude < excludeCount; exclude++)
        {
            excludes[exclude] = $"excluded{exclude:D5}.txt";
        }

        scenario._xml.Append(
            $"""<ItemGroup><Source Include="{string.Join(";", includes)}" Exclude="{string.Join(";", excludes)}"><Trace>{InitialTrace}</Trace></Source></ItemGroup>""");

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateGlobs(LazyItemGlobBenchmark.WorkloadKind workload, int fileCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fileCount, 1);

        LazyItemBenchmarkScenario scenario = new();

        for (int file = 0; file < fileCount; file++)
        {
            scenario.Files.Add(Path.Combine("src", $"dir{file % 10}", $"file{file:D5}.cs"));
        }

        scenario.Files.Sort(StringComparer.OrdinalIgnoreCase);
        string glob = Path.Combine("src", "**", "*.cs");
        scenario.AppendGlob(glob, InitialTrace);

        switch (workload)
        {
            case LazyItemGlobBenchmark.WorkloadKind.EarlyRead:
            case LazyItemGlobBenchmark.WorkloadKind.ManyRemovesAfterRead:
                scenario.AppendReadCount();
                break;

            case LazyItemGlobBenchmark.WorkloadKind.ManyRemovesAfterBookmark:
                scenario._xml.Append("""<ItemGroup><Captured Include="@(Source)" /></ItemGroup>""");

                foreach ((string include, string trace, int order) in scenario._source)
                {
                    scenario._otherItems.Add(("Captured", include, trace, scenario._nextOrder++));
                }

                break;
        }

        switch (workload)
        {
            case LazyItemGlobBenchmark.WorkloadKind.IncludeOnly:
                break;

            case LazyItemGlobBenchmark.WorkloadKind.RemoveAll:
            case LazyItemGlobBenchmark.WorkloadKind.Reinclude:
            case LazyItemGlobBenchmark.WorkloadKind.EarlyRead:
                scenario.AppendRemove(glob, static _ => true);
                break;

            case LazyItemGlobBenchmark.WorkloadKind.SubtreeRemove:
                string subtree = Path.Combine("src", "dir0");
                scenario.AppendRemove(Path.Combine(subtree, "**", "*.cs"), include => Path.GetDirectoryName(include) == subtree);
                break;

            case LazyItemGlobBenchmark.WorkloadKind.ManyRemoves:
            case LazyItemGlobBenchmark.WorkloadKind.ManyRemovesAfterRead:
            case LazyItemGlobBenchmark.WorkloadKind.ManyRemovesAfterBookmark:
                for (int file = 0; file < fileCount; file++)
                {
                    string name = $"file{file:D5}.cs";
                    scenario.AppendRemove(Path.Combine("src", "**", name), include => Path.GetFileName(include) == name);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(workload));
        }

        if (workload is LazyItemGlobBenchmark.WorkloadKind.Reinclude)
        {
            scenario.AppendGlob(glob, "reinserted");
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateUpdateReevaluation(
        LazyItemUpdateReevaluationBenchmark.WorkloadKind workload, int itemCount, int reevaluationCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, OperationCount * 3);
        ArgumentOutOfRangeException.ThrowIfNegative(reevaluationCount);
        LazyItemBenchmarkScenario scenario = new();
        scenario.AppendItems(0, itemCount);

        for (int operation = 0; operation < OperationCount; operation++)
        {
            int first = operation * 2;
            int pending = first + 1;
            int second = OperationCount * 2 + operation;
            string firstPath = Path.Combine(Path.GetTempPath(), ItemName(first));
            string pendingPath = Path.Combine(Path.GetTempPath(), ItemName(pending));
            string secondPath = Path.Combine(Path.GetTempPath(), ItemName(second));
            scenario.AppendUpdate(ItemName(pending), index => index == pending);
            switch (workload)
            {
                case LazyItemUpdateReevaluationBenchmark.WorkloadKind.Disjoint:
                    scenario.AppendUpdate($"{firstPath};{secondPath}", index => index == first || index == second);
                    break;
                case LazyItemUpdateReevaluationBenchmark.WorkloadKind.LateOverlap:
                    scenario.AppendUpdate($"{firstPath};{pendingPath}", index => index == first || index == pending);
                    break;
                case LazyItemUpdateReevaluationBenchmark.WorkloadKind.DuplicateNormalizedKeys:
                    string alternatePath = Path.Combine(Path.GetTempPath(), ".", ItemName(first));
                    scenario.AppendUpdate($"{firstPath};{alternatePath}", index => index == first);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(workload));
            }
        }

        scenario.AppendBookmark(0);
        for (int reevaluation = 0; reevaluation < reevaluationCount; reevaluation++)
        {
            // Different later Remove glob patterns require re-evaluating the same Update operations.
            scenario.AppendRemove($"missing{reevaluation}*.tmp", static _ => false);
            scenario.AppendReadCount();
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateRemoveCollection(bool globRemoves, int operationCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(operationCount, 1);
        LazyItemBenchmarkScenario scenario = new();
        scenario.AppendItem("Source", "kept.cs", InitialTrace);
        for (int operation = 0; operation < operationCount; operation++)
        {
            string pattern = globRemoves ? $"missing{operation}*.tmp" : $"missing{operation}.tmp";
            scenario.AppendRemove(pattern, static _ => false);
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateMetadataCollection(bool updates, int operationCount, int metadataCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(operationCount, 1);
        if (metadataCount is not (0 or 1 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(metadataCount));
        }

        LazyItemBenchmarkScenario scenario = new();
        scenario.AppendItem("Lookup", "lookup", InitialTrace);
        if (updates)
        {
            scenario.AppendItem("Source", ItemName(0), string.Empty);
        }

        for (int operation = 0; operation < operationCount; operation++)
        {
            string include = ItemName(updates ? 0 : operation);
            string attribute = updates ? "Update" : "Include";
            scenario._xml.Append($"""<ItemGroup><Source {attribute}="{include}">""");
            string trace = string.Empty;
            if (metadataCount == 1)
            {
                scenario._xml.Append("<Trace>r</Trace>");
                trace = InitialTrace;
            }
            else if (metadataCount == 4)
            {
                scenario._xml.Append(
                    "<M1>@(Lookup)</M1><M2>%(Identity)</M2><M3>r</M3><Trace>%(M1)%(M2)%(M3)</Trace>");
                trace = $"lookup{include}r";
            }

            scenario._xml.Append("</Source></ItemGroup>");
            if (updates)
            {
                var previous = scenario._source[0];
                scenario._source[0] = (previous.Include, trace, previous.Order);
            }
            else
            {
                scenario._source.Add((include, trace, scenario._nextOrder++));
            }
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateRemoveGlobPatterns(
        LazyItemRemoveGlobPatternBenchmark.WorkloadKind workload, int removeCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(removeCount, 1);
        LazyItemBenchmarkScenario scenario = new();
        for (int file = 0; file < LazyItemRemoveGlobPatternBenchmark.FileCount; file++)
        {
            scenario.Files.Add(Path.Combine("src", $"dir{file % 10}", $"file{file:D5}.cs"));
        }

        scenario.Files.Sort(StringComparer.OrdinalIgnoreCase);
        scenario.AppendGlob(Path.Combine("src", "**", "*.cs"), InitialTrace);
        scenario._xml.Append($"""<ItemGroup><Source Update="{scenario.Files[0]}"><Track>true</Track></Source></ItemGroup>""");
        scenario.AppendBookmark(0);
        bool equivalent = workload is LazyItemRemoveGlobPatternBenchmark.WorkloadKind.EquivalentAfterRead
            or LazyItemRemoveGlobPatternBenchmark.WorkloadKind.EquivalentAfterBookmark;
        bool read = workload is LazyItemRemoveGlobPatternBenchmark.WorkloadKind.EquivalentAfterRead
            or LazyItemRemoveGlobPatternBenchmark.WorkloadKind.GrowingAfterRead;

        for (int operation = 0; operation < removeCount; operation++)
        {
            string first = Path.Combine("src", "**", "missing0*.tmp");
            string second = Path.Combine("src", "**", "missing1*.tmp");
            string pattern = equivalent
                ? (operation % 2 == 0 ? $"{first};{second};{first}" : $"{second};{first};{second}")
                : Path.Combine("src", "**", $"missing{operation}*.tmp");
            scenario.AppendRemove(pattern, static _ => false);
            if (read)
            {
                scenario.AppendReadCount();
            }
            else
            {
                scenario.AppendBookmark(operation + 1);
            }
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateMetadataRemove(int sourceCount, bool sharedFirstValue)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceCount, 1);
        const int targetCount = 512;
        LazyItemBenchmarkScenario scenario = new();
        for (int source = 0; source < sourceCount; source++)
        {
            string group = sharedFirstValue ? "shared" : $"group{source}";
            string key = sharedFirstValue ? $"key{source}" : "shared";
            string include = $"key{source}";
            scenario._xml.Append(
                $"""<ItemGroup><RemoveKey Include="{include}"><Group>{group}</Group><Key>{key}</Key><Trace>r</Trace></RemoveKey></ItemGroup>""");
            scenario._otherItems.Add(("RemoveKey", include, InitialTrace, scenario._nextOrder++));
        }

        HashSet<string> matches = [with(StringComparer.Ordinal)];
        for (int target = 0; target < targetCount; target++)
        {
            int source = target / 2 % sourceCount;
            string group = sharedFirstValue ? "shared" : $"group{source}";
            string key = target % 2 != 0 ? "missing" : sharedFirstValue ? $"key{source}" : "shared";
            string include = ItemName(target);
            scenario._xml.Append(
                $"""<ItemGroup><Source Include="{include}"><Group>{group}</Group><Key>{key}</Key><Trace>r</Trace></Source></ItemGroup>""");
            scenario._source.Add((include, InitialTrace, scenario._nextOrder++));
            if (target % 2 == 0)
            {
                matches.Add(include);
            }
        }

        scenario._xml.Append(
            """<ItemGroup><Source Remove="@(RemoveKey)" MatchOnMetadata="Group;Key;Trace" /></ItemGroup>""");
        scenario._source.RemoveAll(item => matches.Contains(item.Include));
        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateSnapshots(LazyItemSnapshotBenchmark.WorkloadKind workload, int itemCount, int bookmarkCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, OperationCount);
        ArgumentOutOfRangeException.ThrowIfNegative(bookmarkCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bookmarkCount, OperationCount);

        if (bookmarkCount != 0 && OperationCount % bookmarkCount != 0)
        {
            throw new ArgumentException("Reference count must divide the operation count.", nameof(bookmarkCount));
        }

        LazyItemBenchmarkScenario scenario = new();
        if (workload is not LazyItemSnapshotBenchmark.WorkloadKind.Append)
        {
            scenario.AppendItems(0, itemCount);
        }

        for (int operation = 0; operation < OperationCount; operation++)
        {
            switch (workload)
            {
                case LazyItemSnapshotBenchmark.WorkloadKind.Append:
                    int start = operation * itemCount / OperationCount;
                    int end = (operation + 1) * itemCount / OperationCount;
                    scenario.AppendItems(start, end);
                    break;

                case LazyItemSnapshotBenchmark.WorkloadKind.SparseUpdates:
                    scenario.AppendUpdate(ItemName(0), static index => index == 0);
                    break;

                case LazyItemSnapshotBenchmark.WorkloadKind.DenseUpdates:
                    scenario.AppendUpdate("*", static _ => true);
                    break;

                default:
                    Assumed.Unreachable($"Unexpected workload: {workload}");
                    break;
            }

            if (bookmarkCount > 0 && (operation + 1) % (OperationCount / bookmarkCount) == 0)
            {
                scenario.AppendBookmark(operation);
            }
        }

        return scenario.Complete();
    }

    private void AppendItems(int start, int end, bool duplicates = false, bool individualIncludes = false)
    {
        _xml.Append("<ItemGroup>");

        StringBuilder includes = new();
        for (int item = start; item < end; item++)
        {
            string include = ItemName(item);

            if (item == 0)
            {
                _xml.Append($"<Source Include=\"{include}\"><Trace>{InitialTrace}</Trace><Track>true</Track></Source>");
            }
            else if (individualIncludes)
            {
                _xml.Append($"<Source Include=\"{include}\"><Trace>{InitialTrace}</Trace></Source>");
            }
            else
            {
                includes.Append($"{include};");
            }

            _source.Add((include, InitialTrace, _nextOrder++));

            if (duplicates)
            {
                includes.Append($"./{include};");
                _source.Add(($"./{include}", InitialTrace, _nextOrder++));
            }
        }

        if (includes.Length > 0)
        {
            _xml.Append($"<Source Include=\"{includes}\"><Trace>{InitialTrace}</Trace></Source>");
        }

        _xml.Append("</ItemGroup>");
    }

    private void AppendUpdate(string itemSpec, Predicate<int> match)
    {
        _xml.Append($"<ItemGroup><Source Update=\"{itemSpec}\"><Trace>%(Trace)x</Trace></Source></ItemGroup>");

        for (int index = 0; index < _source.Count; index++)
        {
            if (match(index))
            {
                (string? include, string? trace, int order) = _source[index];
                _source[index] = (include, trace + "x", order);
            }
        }
    }

    private void AppendItem(string itemType, string include, string trace)
    {
        _xml.Append($"""<ItemGroup><{itemType} Include="{include}"><Trace>{trace}</Trace></{itemType}></ItemGroup>""");

        if (itemType == "Source")
        {
            _source.Add((include, trace, _nextOrder++));
        }
        else
        {
            _otherItems.Add((itemType, include, trace, _nextOrder++));
        }
    }

    private void AppendGlob(string glob, string trace)
    {
        _xml.Append($"""<ItemGroup><Source Include="{glob}"><Trace>{trace}</Trace></Source></ItemGroup>""");

        foreach (string file in Files)
        {
            _source.Add((file, trace, _nextOrder++));
        }
    }

    private void AppendReadCount()
        // A wrong condition read must produce unexpected output instead of silently skipping an empty probe.
        => _xml.Append(
            $"""<ItemGroup Condition="'@(Source->Count())' != '{_source.Count}'"><ReadMismatch Include="incorrect-count" /></ItemGroup>""");

    private void AppendRemove(string itemSpec, Predicate<string> match)
    {
        _xml.Append($"<ItemGroup><Source Remove=\"{itemSpec}\" /></ItemGroup>");
        _source.RemoveAll(item => match(item.Include));
    }

    private void AppendBookmark(int operation)
    {
        string itemType = $"Snapshot{operation}";
        _xml.Append($"<ItemGroup><{itemType} Include=\"@(Source->WithMetadataValue('Track', 'true'))\" /></ItemGroup>");
        (string? include, string? trace, _) = _source[0];
        _otherItems.Add((itemType, include, trace, _nextOrder++));
    }

    private LazyItemBenchmarkScenario Complete()
    {
        _xml.Append("</Project>");
        ProjectXml = _xml.ToString();

        // Final output is ordered by originating Include declaration, not by item type or snapshot item collection.
        List<(string ItemType, string Include, string Trace, int Order)> ordered = [];
        foreach ((string? include, string? trace, int order) in _source)
        {
            ordered.Add(("Source", include, trace, order));
        }

        ordered.AddRange(_otherItems);
        ordered.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        foreach ((string? itemType, string? include, string? trace, int order) in ordered)
        {
            ExpectedItems.Add((itemType, include, trace));
        }

        return this;
    }

    private static string ItemName(int index)
        => $"item{index:D6}.{(index % 10 == 0 ? "cs" : "txt")}";
}
