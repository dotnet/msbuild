// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Build.Logging;

/// <summary>
/// Holds Terminal Logger presentation state for a project correlated by
/// <see cref="BuildEventTracker"/>.
/// </summary>
/// <remarks>
/// Build facts remain in <see cref="BuildEventTracker"/>.
/// This type stores terminal timing, formatted output, and summary exclusions.
/// </remarks>
internal sealed class TerminalProjectInfo
{
    private List<TerminalBuildMessage>? _buildMessages;
    private BuildEventTracker.ProjectSnapshot _projectSnapshot;
    private int _warningsExcludedFromSummaryCount;

    /// <summary>
    /// Initializes a new <see cref="TerminalProjectInfo"/> for the tracked project.
    /// </summary>
    /// <param name="project">The tracked project.</param>
    /// <param name="stopwatch">The stopwatch used for terminal rendering.</param>
    public TerminalProjectInfo(BuildEventTracker.ProjectSnapshot project, StopwatchAbstraction stopwatch)
    {
        _projectSnapshot = project;
        Stopwatch = stopwatch;
        Stopwatch.Start();
    }

    /// <summary>
    /// The full path to the project file.
    /// </summary>
    public string? ProjectFile => _projectSnapshot.EvaluationProjectFile;

    /// <summary>
    /// A stopwatch to time the build of the project.
    /// </summary>
    public StopwatchAbstraction Stopwatch { get; }

    /// <summary>
    /// The target framework of the project or null if not multi-targeting.
    /// </summary>
    public string? TargetFramework => _projectSnapshot.TargetFramework;

    /// <summary>
    /// The runtime identifier of the project or null if platform-agnostic.
    /// </summary>
    public string? RuntimeIdentifier => _projectSnapshot.RuntimeIdentifier;

    /// <summary>
    /// True if the project built successfully; otherwise false.
    /// </summary>
    public bool Succeeded => _projectSnapshot.Succeeded == true;

    /// <summary>
    /// The number of errors included in the terminal summary.
    /// </summary>
    public int SummaryErrorCount => _projectSnapshot.ErrorCount;

    /// <summary>
    /// The number of warnings included in the terminal summary.
    /// </summary>
    public int SummaryWarningCount
    {
        get
        {
            Debug.Assert(_projectSnapshot.WarningCount >= _warningsExcludedFromSummaryCount);
            return _projectSnapshot.WarningCount - _warningsExcludedFromSummaryCount;
        }
    }

    /// <summary>
    /// True when the project has error or warning build messages; otherwise false.
    /// </summary>
    public bool HasSummaryDiagnostics => SummaryErrorCount > 0 || SummaryWarningCount > 0;

    /// <summary>
    /// Full path to the primary output of the project, if known.
    /// </summary>
    public ReadOnlyMemory<char>? OutputPath { get; set; }

    /// <summary>
    /// Full path to the 'root' of this project's source control repository, if known.
    /// </summary>
    public ReadOnlyMemory<char>? SourceRoot { get; set; }

    /// <summary>
    /// True when the project has run target with name "_TestRunStart" defined in <see cref="TerminalLogger._testStartTarget"/>.
    /// </summary>
    public bool IsTestProject { get; set; }

    /// <summary>
    /// True when the project has run target with name "_CachePluginRunStart".
    /// </summary>
    public bool IsCachePluginProject { get; set; }

    /// <summary>
    /// A lazily initialized list of build messages/warnings/errors raised during the build.
    /// </summary>
    public IReadOnlyList<TerminalBuildMessage>? BuildMessages => _buildMessages;

    /// <summary>
    /// Adds a build message of the given severity to <see cref="BuildMessages"/>.
    /// </summary>
    public void AddBuildMessage(TerminalMessageSeverity severity, string message)
    {
        _buildMessages ??= [];
        _buildMessages.Add(new TerminalBuildMessage(severity, message));
    }

    internal void Complete(BuildEventTracker.ProjectSnapshot projectSnapshot)
    {
        UpdateSnapshot(projectSnapshot);
        Stopwatch.Stop();
    }

    internal void UpdateSnapshot(BuildEventTracker.ProjectSnapshot projectSnapshot)
    {
        Debug.Assert(projectSnapshot.ContextKey == _projectSnapshot.ContextKey);
        _projectSnapshot = projectSnapshot;
    }

    internal void ExcludeWarningFromSummary() => _warningsExcludedFromSummaryCount++;

    internal void ResumeTiming() => Stopwatch.Start();

    internal void YieldTiming() => Stopwatch.Stop();

    /// <summary>
    /// Filters the build messages to only include errors and warnings.
    /// </summary>
    /// <returns>A sequence of error and warning build messages.</returns>
    public IEnumerable<TerminalBuildMessage> GetBuildErrorAndWarningMessages()
    {
        return BuildMessages is null
            ? []
            : BuildMessages.Where(message =>
                message.Severity is TerminalMessageSeverity.Error or TerminalMessageSeverity.Warning);
    }
}
