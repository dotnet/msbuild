// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    public string Target { get; }
    public Stopwatch Stopwatch { get; }

    public NodeStatus(string project, string? targetFramework, string target, Stopwatch stopwatch)
    {
        Project = project;
        TargetFramework = targetFramework;
        Target = target;
        Stopwatch = stopwatch;
    }

    /// <summary>
    /// Equality is based on the project, target framework, and target, but NOT the elapsed time.
    /// </summary>
    public override bool Equals(object? obj) =>
        obj is NodeStatus status &&
        Project == status.Project &&
        TargetFramework == status.TargetFramework &&
        Target == status.Target;

    public override string ToString()
    {
        string duration = Stopwatch.Elapsed.TotalSeconds.ToString("F1");

        return string.IsNullOrEmpty(TargetFramework)
            ? ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectBuilding_NoTF",
                TerminalLogger.Indentation,
                Project,
                Target,
                duration)
            : ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectBuilding_WithTF",
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
