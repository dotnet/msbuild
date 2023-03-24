// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

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
    private List<LazyFormattedBuildEventArgs>? BuildMessages { get; set; }

    public void AddBuildMessage(LazyFormattedBuildEventArgs eventArgs)
    {
        BuildMessages ??= new List<LazyFormattedBuildEventArgs>();
        BuildMessages.Add(eventArgs);
    }

    public IEnumerable<(BuildMessageSeverity, string)> EnumerateBuildMessages()
    {
        if (BuildMessages is not null)
        {
            foreach (LazyFormattedBuildEventArgs eventArgs in BuildMessages)
            {
                if (eventArgs.Message is not null)
                {
                    if (eventArgs is BuildWarningEventArgs warningEventArgs)
                    {
                        yield return (BuildMessageSeverity.Warning, EventArgsFormatting.FormatEventMessage(warningEventArgs, false));
                    }
                    else if (eventArgs is BuildErrorEventArgs errorEventArgs)
                    {
                        yield return (BuildMessageSeverity.Error, EventArgsFormatting.FormatEventMessage(errorEventArgs, false));
                    }
                }
            }
        }
    }
}
