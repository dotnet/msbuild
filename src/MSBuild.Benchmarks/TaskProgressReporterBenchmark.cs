// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures the per-call cost of the task progress reporting protocol.
/// </summary>
/// <remarks>
/// Adopting tasks call <see cref="ITaskProgressReporter.Report"/> once per unit of work, so the
/// cost of a single call decides whether a high-volume task such as <c>Copy</c> can afford to
/// report at all. Three sinks are measured because a task sees all three in practice: a host that
/// does not support progress at all, a supporting host with no logging sink attached, and a
/// supporting host whose forwarding is throttled between updates.
/// </remarks>
[MemoryDiagnoser]
public class TaskProgressReporterBenchmark
{
    /// <summary>
    /// A status string that already exists on the caller's stack, matching the adoption rule that
    /// forbids formatting a new string per reported item.
    /// </summary>
    private const string ExistingStatus = @"obj\Debug\net10.0\Some.Assembly.dll";

    private readonly TaskProgressManager _manager = new();

    private ITaskProgressReporter _unsupportedHost = null!;
    private ITaskProgressReporter _noLoggingSink = null!;
    private ITaskProgressReporter _throttled = null!;
    private long _completed;
    [GlobalSetup]
    public void Setup()
    {
        BuildEventContext context = new(1, 2, 3, 4, 5, 6, 7);

        _unsupportedHost = new DefaultEngineServices().CreateTaskProgressReporter("Benchmark", TaskProgressUnit.Items);
        _noLoggingSink = _manager.CreateReporter("Benchmark", TaskProgressUnit.Items, context, logEvent: null);
        _throttled = _manager.CreateReporter("Benchmark", TaskProgressUnit.Items, context, static _ => { });
    }

    /// <summary>
    /// The cost a task pays on a host that does not implement progress at all. This is what every
    /// adopting task costs existing hosts, so it must be effectively free.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Report_UnsupportedHost() => _unsupportedHost.Report(new TaskProgressUpdate(++_completed, 1000, ExistingStatus));

    /// <summary>
    /// A supporting host with no logging sink. Measures the bookkeeping cost alone.
    /// </summary>
    [Benchmark]
    public void Report_NoLoggingSink() => _noLoggingSink.Report(new TaskProgressUpdate(++_completed, 1000, ExistingStatus));

    /// <summary>
    /// A supporting host in steady state. All but the first call in each throttle window are
    /// discarded before any event is constructed, so this is the path nearly every report takes.
    /// </summary>
    [Benchmark]
    public void Report_Throttled() => _throttled.Report(new TaskProgressUpdate(++_completed, 1000, ExistingStatus));

    /// <summary>
    /// The marginal cost of a report that survives the throttle and is forwarded, measured through
    /// the event that forwarding allocates.
    /// </summary>
    [Benchmark]
    public BuildEventArgs Report_ForwardedEvent()
        => new TaskProgressUpdatedEventArgs(1, ++_completed, _completed, 1000, ExistingStatus);

    /// <summary>
    /// Creating and closing an operation. Adopting tasks pay this once per operation, not per item.
    /// </summary>
    [Benchmark]
    public void CreateAndComplete()
    {
        ITaskProgressReporter reporter = _manager.CreateReporter(
            "Benchmark",
            TaskProgressUnit.Items,
            new BuildEventContext(1, 2, 3, 4, 5, 6, 7),
            logEvent: null);

        reporter.Complete();
    }

    /// <summary>
    /// An <see cref="EngineServices"/> that overrides nothing, which is how a host that predates
    /// progress reporting behaves.
    /// </summary>
    private sealed class DefaultEngineServices : EngineServices
    {
    }
}
