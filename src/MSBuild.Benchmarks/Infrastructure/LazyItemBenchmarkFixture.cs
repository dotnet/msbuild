// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
///  Owns real project data and parsed declarations without including their setup in timed item evaluation.
/// </summary>
/// <remarks>
///  Each cold invocation owns fresh evaluator state and reference captures. CPU-only scenarios also own a
///  fresh evaluation context; glob scenarios reuse a setup-warmed file cache over an owned bounded tree.
///  The project data remains empty: output collectors count or validate the results without saving them
///  into the project's final item table.
/// </remarks>
internal sealed class LazyItemBenchmarkFixture : IDisposable
{
    private readonly ProjectCollection _collection;
    private readonly Project _project;
    private readonly ILoggingService _loggingService;
    private readonly EvaluationLoggingContext _loggingContext;
    private readonly List<ProjectItemGroupElement> _groups;
    private readonly LazyItemBenchmarkScenario _scenario;
    private readonly TemporaryDirectory? _directory;
    private readonly EvaluationContext? _sharedContext;
    private readonly CountingItemCollector _countingCollector = new();
    private LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition> _cachedEvaluator = null!;
    private List<ProjectItemGroupElement> _readGroups = null!;

    public LazyItemBenchmarkFixture(LazyItemBenchmarkScenario scenario)
    {
        _scenario = scenario;
        _collection = new ProjectCollection();
        ProjectRootElement root = ProjectRootElement.Create(_collection);
        if (scenario.Files.Count > 0)
        {
            _directory = new TemporaryDirectory(nameof(LazyItemBenchmarkFixture));
            foreach (string file in scenario.Files)
            {
                _directory.WriteFile(file, string.Empty);
            }

            _sharedContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        }

        root.FullPath = _directory?.GetPath("benchmark.proj") ?? Path.Combine(Path.GetTempPath(), "MSBuild.Benchmarks.LazyItems.proj");
        _project = new Project(root, globalProperties: null, toolsVersion: null, _collection,
            ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition);

        // Initialize the real data with an empty project, then load declarations without reevaluating them.
        using StringReader text = new(scenario.ProjectXml);
        using XmlReader reader = XmlReader.Create(text);
        root.ReloadFrom(reader, throwIfUnsavedChanges: false);
        _groups = [.. root.ItemGroups];

        _loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        _loggingContext = new EvaluationLoggingContext(
            _loggingService, new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1), root.FullPath);
    }

    /// <summary>
    ///  Processes declarations and fully consumes the final and captured historical item sequences.
    /// </summary>
    /// <returns>
    ///  The total materialized item count, including the small historical capture outputs.
    /// </returns>
    public int Evaluate()
        => CountItems(Record());

    /// <summary>
    ///  Records a fresh evaluator, including state demanded by conditions, without requesting final output.
    /// </summary>
    /// <returns>
    ///  The evaluator retained by the benchmark consumer.
    /// </returns>
    public LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition> Record()
    {
        var evaluator = CreateEvaluator();
        evaluator.ProcessItemGroups(_groups, _project.Xml.DirectoryPath);
        return evaluator;
    }

    /// <summary>
    ///  Checks identities, ordering, metadata, and counts with an independent setup-time workload model.
    /// </summary>
    public void Validate()
        => Validate(Record());

    /// <summary>
    ///  Creates and primes the evaluator used only by repeated-read benchmarks.
    /// </summary>
    public void PrepareCachedReads()
    {
        _cachedEvaluator = CreateEvaluator();
        _cachedEvaluator.ProcessItemGroups(_groups, _project.Xml.DirectoryPath);
        Validate(_cachedEvaluator);

        ProjectItemGroupElement read = _project.Xml.AddItemGroup();
        read.Condition = $"'@(Source->Count())' != '{_scenario.SourceCount}'";
        ProjectItemElement mismatch = _project.Xml.CreateItemElement("ReadMismatch", "incorrect-count");
        read.AppendChild(mismatch);
        _readGroups = [read];

        // The false-condition load setting ensures successful reads add no operations or items.
        ReadMatchedPrefix();
        Validate(_cachedEvaluator);
    }

    public int ReadMaterializedPrefix()
        => CountItems(_cachedEvaluator);

    public void ReadMatchedPrefix()
        => _cachedEvaluator.ProcessItemGroups(_readGroups, _project.Xml.DirectoryPath);

    public void Dispose()
    {
        _loggingService.ShutdownComponent();
        _collection.Dispose();
        _directory?.Dispose();
    }

    private LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition> CreateEvaluator()
        => new(
            _project.TestOnlyGetPrivateData,
            new ProjectItem.ProjectItemFactory(_project),
            _loggingContext,
            new EvaluationProfiler(shouldTrackElements: false),
            _sharedContext ?? EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated),
            evaluatedItemElementRecorder: default);

    private int CountItems(LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition> evaluator)
    {
        _countingCollector.Reset();
        evaluator.AddAllItems(_countingCollector);
        return _countingCollector.Count;
    }

    private void Validate(LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition> evaluator)
    {
        ValidatingItemCollector collector = new(_scenario, _project.FullPath);
        evaluator.AddAllItems(collector);
        if (collector.Count != _scenario.ExpectedItems.Count)
        {
            throw new InvalidOperationException($"Lazy item evaluation produced {collector.Count} items; expected {_scenario.ExpectedItems.Count}.");
        }
    }

    private sealed class CountingItemCollector : IItemEvaluationCollector<ProjectItem>
    {
        public bool ShouldEvaluateForDesignTime => false;

        public int Count { get; private set; }

        public void Reset() => Count = 0;

        public void AddItem(ProjectItem item) => Count++;

        public void AddToAllEvaluatedItemsList(ProjectItem item)
            => throw new InvalidOperationException("Unexpected design-time item output.");

        public void AddItemIgnoringCondition(ProjectItem item)
            => throw new InvalidOperationException("Unexpected condition-ignoring item output.");
    }

    private sealed class ValidatingItemCollector(LazyItemBenchmarkScenario scenario, string projectPath) : IItemEvaluationCollector<ProjectItem>
    {
        public bool ShouldEvaluateForDesignTime => false;

        public int Count { get; private set; }

        public void AddItem(ProjectItem item)
        {
            if (Count >= scenario.ExpectedItems.Count)
            {
                throw new InvalidOperationException("Lazy item evaluation produced more items than expected.");
            }

            var expected = scenario.ExpectedItems[Count++];
            if (item.ItemType != expected.ItemType
                || item.EvaluatedInclude != expected.Include
                || item.GetMetadataValue("Trace") != expected.Trace
                || item.Xml.Include.Length == 0
                || item.GetMetadataValue("DefiningProjectFullPath") != projectPath)
            {
                throw new InvalidOperationException(
                    $"Lazy item result {Count - 1} differs: expected {expected}, "
                    + $"actual ({item.ItemType}, {item.EvaluatedInclude}, {item.GetMetadataValue("Trace")}).");
            }
        }

        public void AddToAllEvaluatedItemsList(ProjectItem item)
            => throw new InvalidOperationException("Unexpected design-time item output.");

        public void AddItemIgnoringCondition(ProjectItem item)
            => throw new InvalidOperationException("Unexpected condition-ignoring item output.");
    }
}
