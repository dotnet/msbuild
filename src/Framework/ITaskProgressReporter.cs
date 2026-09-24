// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Reports progress for one task operation.
    /// </summary>
    /// <remarks>
    /// Progress updates are transient snapshots. The build engine may coalesce or discard intermediate updates.
    /// Disposing a reporter without first completing, canceling, or failing it abandons the operation.
    /// </remarks>
    public interface ITaskProgressReporter : IProgress<TaskProgressUpdate>, IDisposable
    {
        /// <summary>
        /// Reports that the operation completed successfully.
        /// </summary>
        /// <param name="summary">An optional final summary.</param>
        void Complete(string? summary = null);

        /// <summary>
        /// Reports that the operation was canceled.
        /// </summary>
        /// <param name="summary">An optional final summary.</param>
        void Cancel(string? summary = null);

        /// <summary>
        /// Reports that the operation failed.
        /// </summary>
        /// <param name="summary">An optional final summary.</param>
        /// <remarks>
        /// This method does not fail the task. Tasks must continue to report errors and return failure through the existing task APIs.
        /// </remarks>
        void Fail(string? summary = null);
    }
}
