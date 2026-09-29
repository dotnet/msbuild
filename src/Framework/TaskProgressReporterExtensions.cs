// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Threading;

namespace Microsoft.Build.Framework;

/// <summary>
/// Helpers for <see cref="ITaskProgressReporter"/>.
/// </summary>
public static class TaskProgressReporterExtensions
{
    /// <summary>
    /// Ends the operation with the outcome that matches how the task finished.
    /// </summary>
    /// <param name="reporter">The reporter to close.</param>
    /// <param name="succeeded"><see langword="true"/> if the work succeeded.</param>
    /// <param name="cancellationToken">The token that cancels the task. A canceled token wins over <paramref name="succeeded"/>.</param>
    /// <param name="summary">An optional final summary.</param>
    /// <remarks>
    /// Calls <see cref="ITaskProgressReporter.Cancel"/> when <paramref name="cancellationToken"/> is canceled,
    /// otherwise <see cref="ITaskProgressReporter.Complete"/> when <paramref name="succeeded"/> is <see langword="true"/>,
    /// otherwise <see cref="ITaskProgressReporter.Fail"/>. Like the terminal members, this has no effect after the
    /// operation has ended, so it is safe to call from every exit path of a task.
    /// </remarks>
    public static void Finish(this ITaskProgressReporter reporter, bool succeeded, CancellationToken cancellationToken = default, string? summary = null)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            reporter.Cancel(summary);
        }
        else if (succeeded)
        {
            reporter.Complete(summary);
        }
        else
        {
            reporter.Fail(summary);
        }
    }
}
