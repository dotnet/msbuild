// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Logging;

/// <summary>
/// Observes diagnostics synchronously on the node executing a task, before logging is queued
/// or warning policy is applied. This does not drain events from remote nodes.
/// </summary>
internal interface ITaskCacheDiagnosticSource
{
    TaskCacheDiagnosticCapture CaptureTaskDiagnostics(BuildEventContext context);

    void RecordTaskError(BuildEventContext context);

    void RecordUnsupportedTaskWarning(BuildEventContext context, BuildWarningEventArgs warning);
}
