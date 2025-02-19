// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging.TerminalLogger;

/// <summary>
/// Encapsulates the per-node data shown in live node output.
/// </summary>
internal class NodeStatus
{
    public string Project { get; }
    public string? TargetFramework { get; }
    public TerminalColor TargetPrefixColor { get; } = TerminalColor.Default;
    public string? TargetPrefix { get; }
    public string Target { get; }
    public StopwatchAbstraction Stopwatch { get; }

    /// <summary>
    /// Status of a node that is currently doing work.
    /// </summary>
    /// <param name="project">The project that is written on left side.</param>
    /// <param name="targetFramework">Target framework that is colorized and written on left side after project.</param>
    /// <param name="target">The currently running work, usually the currently running target. Written on right.</param>
    /// <param name="stopwatch">Duration of the current step. Written on right after target.</param>
    public NodeStatus(string project, string? targetFramework, string target, StopwatchAbstraction stopwatch)
    {
#if DEBUG
        if (target.Contains("\x1B"))
        {
            throw new ArgumentException("Target should not contain any escape codes, if you want to colorize target use the other constructor.");
        }
#endif
        Project = project;
        TargetFramework = targetFramework;
        Target = target;
        Stopwatch = stopwatch;
    }

    /// <summary>
    /// Status of a node that is currently doing work.
    /// </summary>
    /// <param name="project">The project that is written on left side.</param>
    /// <param name="targetFramework">Target framework that is colorized and written on left side after project.</param>
    /// <param name="targetPrefixColor">Color for the status of the currently running work written on right.</param>
    /// <param name="targetPrefix">Colorized status for the currently running work, written on right, before target, and separated by 1 space from it.</param>
    /// <param name="target">The currently running work, usually the currently runnig target. Written on right.</param>
    /// <param name="stopwatch">Duration of the current step. Written on right after target.</param>
    public NodeStatus(string project, string? targetFramework, TerminalColor targetPrefixColor, string targetPrefix, string target, StopwatchAbstraction stopwatch)
        : this(project, targetFramework, target, stopwatch)
    {
        TargetPrefixColor = targetPrefixColor;
        TargetPrefix = targetPrefix;
    }

    /// <summary>
    /// Equality is based on the project, target framework, and target, but NOT the elapsed time.
    /// </summary>
    public override bool Equals(object? obj) =>
        obj is NodeStatus status &&
        Project == status.Project &&
        TargetFramework == status.TargetFramework &&
        Target == status.Target &&
        TargetPrefixColor == status.TargetPrefixColor &&
        TargetPrefix == status.TargetPrefix;

    public override string ToString()
    {
        string duration = Stopwatch.ElapsedSeconds.ToString("F1");

        return string.IsNullOrEmpty(TargetFramework)
            ? string.Format("{0}{1} {2} ({3}s)",
                TerminalLogger.Indentation,
                Project,
                Target,
                duration)
            : string.Format("{0}{1} {2} {3} ({4}s)",
                TerminalLogger.Indentation,
                Project,
                AnsiCodes.Colorize(TargetFramework, TerminalLogger.TargetFrameworkColor),
                Target,
                duration);
    }

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }
}
