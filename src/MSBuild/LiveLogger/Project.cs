// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Logging.LiveLogger;

/// <summary>
/// Represents a project being built.
/// </summary>
internal sealed class Project
{
    /// <summary>
    /// A stopwatch to time the build of the project.
    /// </summary>
    public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    /// <summary>
    /// Full path to the primary output of the project, if known.
    /// </summary>
    public ReadOnlyMemory<char>? OutputPath { get; set; }

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

/// <summary>
/// Enumerates the supported message severities.
/// </summary>
internal enum MessageSeverity { Warning, Error }

/// <summary>
/// Represents a piece of diagnostic output (message/warning/error).
/// </summary>
internal record struct BuildMessage(MessageSeverity Severity, string Message)
{ }
