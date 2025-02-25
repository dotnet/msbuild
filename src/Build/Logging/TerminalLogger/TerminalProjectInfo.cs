// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Build.Logging;

/// <summary>
/// Represents a project being built.
/// </summary>
internal sealed class TerminalProjectInfo
{
    private List<TerminalBuildMessage>? _buildMessages;

    /// <summary>
    /// Initialized a new <see cref="TerminalProjectInfo"/> with the given <paramref name="targetFramework"/>.
    /// </summary>
    /// <param name="projectFile">The full path to the project file.</param>
    /// <param name="targetFramework">The target framework of the project or null if not multi-targeting.</param>
    /// <param name="stopwatch">A stopwatch to time the build of the project.</param>
    public TerminalProjectInfo(string projectFile, string? targetFramework, StopwatchAbstraction? stopwatch)
    {
        File = projectFile;
        TargetFramework = targetFramework;

        if (stopwatch is not null)
        {
            stopwatch.Start();
            Stopwatch = stopwatch;
        }
        else
        {
            Stopwatch = SystemStopwatch.StartNew();
        }
    }

    public string File { get; }

    /// <summary>
    /// A stopwatch to time the build of the project.
    /// </summary>
    public StopwatchAbstraction Stopwatch { get; }

    /// <summary>
    /// Full path to the primary output of the project, if known.
    /// </summary>
    public ReadOnlyMemory<char>? OutputPath { get; set; }

    /// <summary>
    /// The target framework of the project or null if not multi-targeting.
    /// </summary>
    public string? TargetFramework { get; }

    /// <summary>
    /// True when the project has run target with name "_TestRunStart" defined in <see cref="TerminalLogger._testStartTarget"/>.
    /// </summary>
    public bool IsTestProject { get; set; }

    /// <summary>
    /// True when the project has run target with name "_CachePluginRunStart".
    /// </summary>
    public bool IsCachePluginProject { get; set; }

    /// <summary>
    /// True if project built successfully; otherwise false.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// The number of errors raised during the build of the project.
    /// </summary>
    public int ErrorCount { get; private set; }

    /// <summary>
    /// The number of warnings raised during the build of the project.
    /// </summary>
    public int WarningCount { get; private set; }

    /// <summary>
    /// True when the project has error or warning build messages; otherwise false.
    /// </summary>
    public bool HasErrorsOrWarnings => ErrorCount > 0 || WarningCount > 0;

    /// <summary>
    /// A lazily initialized list of build messages/warnings/errors raised during the build.
    /// </summary>
    public IReadOnlyList<TerminalBuildMessage>? BuildMessages => _buildMessages;

    /// <summary>
    /// Adds a build message of the given severity to <see cref="BuildMessages"/>.
    /// </summary>
    public void AddBuildMessage(TerminalMessageSeverity severity, string message)
    {
        _buildMessages ??= new List<TerminalBuildMessage>();
        _buildMessages.Add(new TerminalBuildMessage(severity, message));

        if (severity == TerminalMessageSeverity.Error)
        {
            ErrorCount++;
        }
        else if (severity == TerminalMessageSeverity.Warning)
        {
            WarningCount++;
        }
    }

    /// <summary>
    /// Filters the build messages to only include errors and warnings.
    /// </summary>
    /// <returns>A sequence of error and warning build messages.</returns>
    public IEnumerable<TerminalBuildMessage> GetBuildErrorAndWarningMessages()
    {
        return BuildMessages is null ?
            Enumerable.Empty<TerminalBuildMessage>() :
            BuildMessages.Where(message =>
                message.Severity == TerminalMessageSeverity.Error ||
                message.Severity == TerminalMessageSeverity.Warning);
    }
}
