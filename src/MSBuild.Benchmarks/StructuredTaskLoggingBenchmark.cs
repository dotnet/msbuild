// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures the cost of creating task logging events without materializing their visible messages.
/// This isolates handler capture and structured-state construction from logger rendering.
/// </summary>
[MemoryDiagnoser]
public class StructuredTaskLoggingBenchmark
{
    private readonly BenchmarkBuildEngine _enabledEngine = new(logMessages: true);
    private readonly BenchmarkBuildEngine _disabledEngine = new(logMessages: false);
    private TaskLoggingHelper _enabled = null!;
    private TaskLoggingHelper _disabled = null!;
    private string _candidate = null!;
    private string _expected = null!;
    private string _searchPath = null!;
    private int _attempt;
    private string? _originalDisabledFeaturesFromVersion;

    [GlobalSetup]
    public void Setup()
    {
        _originalDisabledFeaturesFromVersion =
            Environment.GetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION");
        Environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        ChangeWaves.ResetStateForTests();

        _enabled = new TaskLoggingHelper(_enabledEngine, "StructuredLoggingBenchmark");
        _disabled = new TaskLoggingHelper(_disabledEngine, "StructuredLoggingBenchmark");
        _candidate = "candidate.dll";
        _expected = "expected.dll";
        _searchPath = "/packages/reference";
        _attempt = 42;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(
            "MSBUILDDISABLEFEATURESFROMVERSION",
            _originalDisabledFeaturesFromVersion);
        ChangeWaves.ResetStateForTests();
    }

    [Benchmark]
    public BuildMessageEventArgs ClassicCompositeOneHole()
    {
        _enabled.LogMessage(MessageImportance.Low, "Considered {0}", _candidate);
        return _enabledEngine.LastMessage!;
    }

    [Benchmark]
    public BuildMessageEventArgs ClassicCompositeTwoHoles()
    {
        _enabled.LogMessage(
            MessageImportance.Low,
            "Considered {0} but expected {1}",
            _candidate,
            _expected);
        return _enabledEngine.LastMessage!;
    }

    [Benchmark]
    public BuildMessageEventArgs ClassicCompositeFourHoles()
    {
        _enabled.LogMessage(
            MessageImportance.Low,
            "Considered {0} but expected {1} under {2} on attempt {3}",
            _candidate,
            _expected,
            _searchPath,
            _attempt);
        return _enabledEngine.LastMessage!;
    }

    [Benchmark]
    public BuildMessageEventArgs StructuredInterpolationOneHole()
    {
        _enabled.LogMessage(MessageImportance.Low, $"Considered {_candidate}");
        return _enabledEngine.LastMessage!;
    }

    [Benchmark]
    public BuildMessageEventArgs StructuredInterpolationTwoHoles()
    {
        _enabled.LogMessage(
            MessageImportance.Low,
            $"Considered {_candidate} but expected {_expected}");
        return _enabledEngine.LastMessage!;
    }

    [Benchmark]
    public BuildMessageEventArgs StructuredInterpolationFourHoles()
    {
        _enabled.LogMessage(
            MessageImportance.Low,
            $"Considered {_candidate} but expected {_expected} under {_searchPath} on attempt {_attempt}");
        return _enabledEngine.LastMessage!;
    }

    [Benchmark]
    public BuildMessageEventArgs? DisabledClassicComposite()
    {
        _disabledEngine.LastMessage = null;
        _disabled.LogMessage(
            MessageImportance.Low,
            "Considered {0} but expected {1}",
            _candidate,
            _expected);
        return _disabledEngine.LastMessage;
    }

    [Benchmark]
    public BuildMessageEventArgs? DisabledStructuredInterpolation()
    {
        _disabledEngine.LastMessage = null;
        _disabled.LogMessage(
            MessageImportance.Low,
            $"Considered {_candidate} but expected {_expected}");
        return _disabledEngine.LastMessage;
    }

    [Benchmark]
    public BuildMessageEventArgs DynamicStructuredEnabled()
    {
        _enabled.LogStructuredMessage(
            MessageImportance.Low,
            "Considered {Candidate} but expected {Expected}",
            _candidate,
            _expected);
        return _enabledEngine.LastMessage!;
    }

    [Benchmark]
    public BuildMessageEventArgs? DynamicStructuredDisabled()
    {
        _disabledEngine.LastMessage = null;
        _disabled.LogStructuredMessage(
            MessageImportance.Low,
            "Considered {Candidate} but expected {Expected}",
            _candidate,
            _expected);
        return _disabledEngine.LastMessage;
    }

    [Benchmark]
    public string StructuredInterpolationAndMaterialize()
    {
        _enabled.LogMessage(
            MessageImportance.Low,
            $"Considered {_candidate} but expected {_expected}");
        return _enabledEngine.LastMessage!.Message!;
    }

    [Benchmark]
    public string ClassicCompositeAndMaterialize()
    {
        _enabled.LogMessage(
            MessageImportance.Low,
            "Considered {0} but expected {1}",
            _candidate,
            _expected);
        return _enabledEngine.LastMessage!.Message!;
    }

    private sealed class BenchmarkBuildEngine(bool logMessages) : EngineServices, IBuildEngine10
    {
        public BuildMessageEventArgs? LastMessage { get; set; }

        public EngineServices EngineServices => this;
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "benchmark.proj";
        public bool IsRunningMultipleNodes => false;
        public bool AllowFailureWithoutError { get; set; }

        public override bool LogsMessagesOfImportance(MessageImportance importance) => logMessages;
        public override bool IsTaskInputLoggingEnabled => false;
        public override bool IsOutOfProcRarNodeEnabled => false;

        public void LogMessageEvent(BuildMessageEventArgs e) => LastMessage = e;
        public void LogErrorEvent(BuildErrorEventArgs e) { }
        public void LogWarningEvent(BuildWarningEventArgs e) { }
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public void LogTelemetry(string eventName, IDictionary<string, string> properties) { }
        public IReadOnlyDictionary<string, string> GetGlobalProperties() =>
            new Dictionary<string, string>();
        public bool ShouldTreatWarningAsError(string warningCode) => false;
        public int RequestCores(int requestedCores) => requestedCores;
        public void ReleaseCores(int coresToRelease) { }
        public void Yield() { }
        public void Reacquire() { }
        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => null!;
        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => null!;
        public void RegisterTaskObject(
            object key,
            object obj,
            RegisteredTaskObjectLifetime lifetime,
            bool allowEarlyCollection) { }

        public bool BuildProjectFile(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs) => false;

        public bool BuildProjectFile(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs,
            string toolsVersion) => false;

        public bool BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersion,
            bool useResultsCache,
            bool unloadProjectsOnCompletion) => false;

        public BuildEngineResult BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] removeGlobalProperties,
            string[] toolsVersion,
            bool returnTargetOutputs) => new(false, null!);
    }
}
