// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging.TerminalLogger;

/// <summary>
/// Encapsulates the per-node data shown in live node output.
/// </summary>
internal record NodeStatus(string Project, string? TargetFramework, string Target, Stopwatch Stopwatch)
{
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
}
