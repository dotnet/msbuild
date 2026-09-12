// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Logging;

internal partial class LoggingService : ITaskCacheDiagnosticSource
{
    // Copy-on-write registration requires no allocations when disabled. Only a
    // matching diagnostic takes the capture's gate to snapshot its payload and phase.
    private TaskCacheDiagnosticCapture[]? _taskDiagnosticCaptures;

    public TaskCacheDiagnosticCapture CaptureTaskDiagnostics(BuildEventContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.NodeId == BuildEventContext.InvalidNodeId
            || context.ProjectContextId == BuildEventContext.InvalidProjectContextId
            || context.TargetId == BuildEventContext.InvalidTargetId
            || context.TaskId == BuildEventContext.InvalidTaskId)
        {
            throw new ArgumentException("A task build event context is required.", nameof(context));
        }

        TaskCacheDiagnosticCapture capture = new(this, context);
        TaskCacheDiagnosticCapture[]? previous;
        TaskCacheDiagnosticCapture[] updated;
        do
        {
            previous = Volatile.Read(ref _taskDiagnosticCaptures);
            int count = previous?.Length ?? 0;
            updated = new TaskCacheDiagnosticCapture[count + 1];
            if (previous is not null)
            {
                Array.Copy(previous, updated, count);
            }

            updated[count] = capture;
        }
        while (Interlocked.CompareExchange(ref _taskDiagnosticCaptures, updated, previous) != previous);

        return capture;
    }

    internal void RemoveTaskDiagnosticCapture(TaskCacheDiagnosticCapture capture)
    {
        TaskCacheDiagnosticCapture[]? previous;
        TaskCacheDiagnosticCapture[]? updated;
        do
        {
            previous = Volatile.Read(ref _taskDiagnosticCaptures);
            if (previous is null)
            {
                return;
            }

            int index = Array.IndexOf(previous, capture);
            if (index < 0)
            {
                return;
            }

            updated = null;
            if (previous.Length > 1)
            {
                updated = new TaskCacheDiagnosticCapture[previous.Length - 1];
                Array.Copy(previous, 0, updated, 0, index);
                Array.Copy(previous, index + 1, updated, index, previous.Length - index - 1);
            }
        }
        while (Interlocked.CompareExchange(ref _taskDiagnosticCaptures, updated, previous) != previous);
    }

    public void RecordTaskError(BuildEventContext context)
    {
        TaskCacheDiagnosticCapture[]? captures = Volatile.Read(ref _taskDiagnosticCaptures);
        if (captures is not null)
        {
            foreach (TaskCacheDiagnosticCapture capture in captures)
            {
                capture.RecordRawError(context);
            }
        }
    }

    public void RecordUnsupportedTaskWarning(BuildEventContext context, BuildWarningEventArgs warning)
    {
        if (warning.GetType() == typeof(BuildWarningEventArgs))
        {
            return;
        }

        TaskCacheDiagnosticCapture[]? captures = Volatile.Read(ref _taskDiagnosticCaptures);
        if (captures is not null)
        {
            foreach (TaskCacheDiagnosticCapture capture in captures)
            {
                capture.RecordDiagnostic(warning, context);
            }
        }
    }

    private static void RecordTaskDiagnostic(object loggingEvent, TaskCacheDiagnosticCapture[] captures)
    {
        BuildEventArgs? buildEvent = loggingEvent switch
        {
            BuildEventArgs directEvent => directEvent,
            KeyValuePair<int, BuildEventArgs> forwardedEvent => forwardedEvent.Value,
            _ => null
        };

        if (buildEvent is not (BuildWarningEventArgs or BuildErrorEventArgs)
            || buildEvent.BuildEventContext is not BuildEventContext context)
        {
            return;
        }

        foreach (TaskCacheDiagnosticCapture capture in captures)
        {
            capture.RecordDiagnostic(buildEvent, context);
        }
    }
}
