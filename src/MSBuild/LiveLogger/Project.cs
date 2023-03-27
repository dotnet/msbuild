// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Logging.LiveLogger;

internal sealed class Project
{
    /// <summary>
    /// A stopwatch to time the build of this project.
    /// </summary>
    public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    /// <summary>
    /// The full path to the primary output of the project, if known.
    /// </summary>
    public ReadOnlyMemory<char>? OutputPath { get; set; }

    /// <summary>
    /// A lazily initialized list of build messages/warnings/errors raised during the build.
    /// </summary>
    public List<string>? BuildMessages { get; private set; }

    public void AddBuildMessage(string message)
    {
        BuildMessages ??= new List<string>();
        BuildMessages.Add(message);
    }
}
