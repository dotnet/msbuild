// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class ProjectConfigurationDescriptionBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private ProjectStartedEventArgs _projectStarted = null!;

    [Params(0, 2)]
    public int DescriptionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _projectCollection = new ProjectCollection();
        ProjectInstance project = new(ProjectRootElement.Create(_projectCollection));
        for (int i = 0; i < 10_000; i++)
        {
            project.AddItem("Compile", $"File{i}.cs");
        }

        for (int i = 0; i < DescriptionCount; i++)
        {
            project.AddItem("ProjectConfigurationDescription", $"Configuration{i}");
        }

        var items = new CopyOnReadEnumerable<ProjectItemInstance, DictionaryEntry>(
            project.Items,
            new object(),
            static item => new DictionaryEntry(item.ItemType, new TaskItem(item)));
        BuildEventContext context = new(1, 2, 3, 4);
        _projectStarted = new ProjectStartedEventArgs(-1, string.Empty, string.Empty, "project.proj", null, null, items, context)
        {
            BuildEventContext = context
        };
    }

    [Benchmark]
    public void ProjectStarted()
    {
        var logger = new ParallelConsoleLogger(LoggerVerbosity.Minimal, static _ => { }, null, null);
        logger.Initialize(new EventSourceSink(), 2);
        logger.ProjectStartedHandler(null, _projectStarted);
        logger.Shutdown();
    }

    [GlobalCleanup]
    public void Cleanup() => _projectCollection.Dispose();
}
