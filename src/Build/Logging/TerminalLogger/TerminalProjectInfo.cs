// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging;

/// <summary>
/// Represents a project being built.
/// </summary>
internal sealed class TerminalProjectInfo
{
    private List<TerminalBuildMessage>? _buildMessages;
    private int _errorCount;
    private int _warningCount;

    /// <summary>
    /// Initializes a new <see cref="TerminalProjectInfo"/> for the tracked project.
    /// </summary>
    /// <param name="project">The tracked project.</param>
    /// <param name="stopwatch">The stopwatch used for terminal rendering.</param>
    public TerminalProjectInfo(BuildEventTracker.ProjectSnapshot project, StopwatchAbstraction stopwatch)
    {
        Id = project.ProjectContextId;
        ProjectFile = project.EvaluationProjectFile;
        TargetFramework = project.TargetFramework;
        RuntimeIdentifier = project.RuntimeIdentifier;
        Stopwatch = stopwatch;
        Stopwatch.Start();
    }

    /// <summary>
    /// The int value of the ProjectContext id of this project execution.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The full path to the project file.
    /// </summary>
    public string? ProjectFile { get; }

    /// <summary>
    /// A stopwatch to time the build of the project.
    /// </summary>
    public StopwatchAbstraction Stopwatch { get; }

    /// <summary>
    /// The target framework of the project or null if not multi-targeting.
    /// </summary>
    public string? TargetFramework { get; }

    /// <summary>
    /// The runtime identifier of the project or null if platform-agnostic.
    /// </summary>
    public string? RuntimeIdentifier { get; }

    /// <summary>
    /// True if the project built successfully; otherwise false.
    /// </summary>
    public bool Succeeded { get; private set; }

    /// <summary>
    /// The number of errors included in the terminal summary.
    /// </summary>
    public int ErrorCount => _errorCount;

    /// <summary>
    /// The number of warnings included in the terminal summary.
    /// </summary>
    public int WarningCount => _warningCount;

    /// <summary>
    /// True when the project has error or warning build messages; otherwise false.
    /// </summary>
    public bool HasErrorsOrWarnings => ErrorCount > 0 || WarningCount > 0;

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

        switch (severity)
        {
            case TerminalMessageSeverity.Error:
                _errorCount++;
                break;
            case TerminalMessageSeverity.Warning:
                _warningCount++;
                break;
        }
    }

    internal void Finish(bool succeeded)
    {
        Succeeded = succeeded;
        Stopwatch.Stop();
    }

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
