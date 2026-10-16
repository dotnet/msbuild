// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
/// A task item that avoids the metadata machinery so progress benchmarks measure the task rather
/// than item cloning.
/// </summary>
internal sealed class TaskItemStub(string itemSpec) : ITaskItem
{
    public string ItemSpec { get; set; } = itemSpec;

    public ICollection MetadataNames => Array.Empty<string>();

    public int MetadataCount => 0;

    public IDictionary CloneCustomMetadata() => new Dictionary<string, string>();

    public void CopyMetadataTo(ITaskItem destinationItem)
    {
    }

    public string GetMetadata(string metadataName) => string.Empty;

    public void RemoveMetadata(string metadataName)
    {
    }

    public void SetMetadata(string metadataName, string metadataValue)
    {
    }
}

/// <summary>
/// A build engine whose <see cref="EngineServices"/> either creates real progress reporters or
/// falls back to the no-op reporter that hosts without progress support return.
/// </summary>
internal sealed class BenchmarkBuildEngine(bool supportsProgress) : IBuildEngine10
{
    public EngineServices EngineServices { get; } = supportsProgress
        ? new ProgressEngineServices()
        : new NoProgressEngineServices();

    public bool ContinueOnError => false;

    public int LineNumberOfTaskNode => 0;

    public int ColumnNumberOfTaskNode => 0;

    public string ProjectFileOfTaskNode => string.Empty;

    public bool IsRunningMultipleNodes => false;

    public bool AllowFailureWithoutError { get; set; }

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
    }

    public void LogTelemetry(string eventName, IDictionary<string, string> properties)
    {
    }

    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => false;

    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => false;

    public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion) => false;

    public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs) => new(false, []);

    public void Yield()
    {
    }

    public void Reacquire()
    {
    }

    public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
    {
    }

    public object? GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => null;

    public object? UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => null;

    public IReadOnlyDictionary<string, string> GetGlobalProperties() => new Dictionary<string, string>();

    public bool ShouldTreatWarningAsError(string warningCode) => false;

    public int RequestCores(int requestedCores) => requestedCores;

    public void ReleaseCores(int coresToRelease)
    {
    }

    /// <summary>
    /// Engine services that override only what a task needs to run, so progress falls back to the
    /// no-op reporter that a host without progress support returns.
    /// </summary>
    private class NoProgressEngineServices : EngineServices
    {
        public override bool LogsMessagesOfImportance(MessageImportance importance) => true;
    }

    /// <summary>
    /// Engine services backed by a real reporter with a logging sink attached, matching what a
    /// task sees inside a build that is rendering progress.
    /// </summary>
    private sealed class ProgressEngineServices : NoProgressEngineServices
    {
        private readonly TaskProgressManager _manager = new();
        private readonly BuildEventContext _context = new(1, 2, 3, 4, 5, 6, 7);

        public override ITaskProgressReporter CreateTaskProgressReporter(string title, TaskProgressUnit unit = TaskProgressUnit.Unspecified)
            => _manager.CreateReporter(title, unit, _context, static _ => { });
    }
}
