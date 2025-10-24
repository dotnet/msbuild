// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging;

/// <summary>
/// Tracks build-level data for the TerminalLogger across an entire build session.
/// </summary>
public sealed class TerminalBuildData
{
    /// <summary>
    /// The timestamp of the build start event.
    /// </summary>
    public DateTime BuildStartTime { get; set; }

    /// <summary>
    /// Number of build errors encountered during the build.
    /// </summary>
    public int BuildErrorsCount { get; set; }

    /// <summary>
    /// Number of build warnings encountered during the build.
    /// </summary>
    public int BuildWarningsCount { get; set; }

    /// <summary>
    /// The project build context corresponding to the Restore initial target, or null if the build is currently not restoring.
    /// </summary>
    public int? RestoreContext { get; set; }

    /// <summary>
    /// True if restore failed and this failure has already been reported.
    /// </summary>
    public bool RestoreFailed { get; set; }

    /// <summary>
    /// True if restore happened and finished.
    /// </summary>
    public bool RestoreFinished { get; set; }

    /// <summary>
    /// Initializes a new instance of TerminalBuildData.
    /// </summary>
    /// <param name="buildStartTime">The timestamp when the build started.</param>
    public TerminalBuildData(DateTime buildStartTime)
    {
        BuildStartTime = buildStartTime;
    }
}