﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Logging.TerminalLogger;

/// <summary>
/// Represents a project being built.
/// </summary>
internal sealed class Project
{
    /// <summary>
    /// Initialized a new <see cref="Project"/> with the given <paramref name="targetFramework"/>.
    /// </summary>
    /// <param name="targetFramework">The target framework of the project or null if not multi-targeting.</param>
    public Project(string? targetFramework, StopwatchAbstraction? stopwatch)
    {
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
    /// A lazily initialized list of build messages/warnings/errors raised during the build.
    /// </summary>
    public List<BuildMessage>? BuildMessages { get; private set; }

    /// <summary>
    /// Adds a build message of the given severity to <see cref="BuildMessages"/>.
    /// </summary>
    public void AddBuildMessage(MessageSeverity severity, string message)
    {
        BuildMessages ??= new List<BuildMessage>();
        BuildMessages.Add(new BuildMessage(severity, message));
    }
}
