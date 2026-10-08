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
    /// <para>
    /// Progress updates are transient snapshots. The build engine may coalesce or discard intermediate updates.
    /// Disposing a reporter without first completing, canceling, or failing it abandons the operation.
    /// </para>
    /// <para>
    /// Progress is presentation data and must never change the outcome of a task. Implementations must therefore
    /// never throw from any member of this interface, and every member is safe to call from any thread. The first
    /// terminal call (<see cref="Complete"/>, <see cref="Cancel"/>, <see cref="Fail"/>, or <see cref="IDisposable.Dispose"/>)
    /// closes the operation; every later call, including a second terminal call or a report after the operation
    /// closed, is silently ignored. Tasks do not need to guard calls with <c>try</c>/<c>catch</c> or track whether
    /// the operation already ended.
    /// </para>
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

        /// <summary>
        /// Atomically adds <paramref name="delta"/> to <see cref="TaskProgressUpdate.Completed"/>, keeping the current total and status.
        /// </summary>
        /// <param name="delta">The amount of work that finished. A negative value decreases the count.</param>
        /// <remarks>
        /// Safe to call concurrently. The result is clamped to the range from zero to <see cref="long.MaxValue"/>.
        /// </remarks>
        void Increment(long delta = 1);

        /// <summary>
        /// Atomically adds <paramref name="delta"/> to <see cref="TaskProgressUpdate.Total"/>, keeping the current completed count and status.
        /// </summary>
        /// <param name="delta">The amount of work that was discovered. An unknown total is treated as zero.</param>
        /// <remarks>
        /// Use this when the total grows while the operation runs. The result is clamped to the range from zero to <see cref="long.MaxValue"/>.
        /// </remarks>
        void AddToTotal(long delta);

        /// <summary>
        /// Sets <see cref="TaskProgressUpdate.Total"/>, keeping the current completed count and status.
        /// </summary>
        /// <param name="total">The total amount of work, or <see langword="null"/> when it is unknown. A negative value is treated as zero.</param>
        void SetTotal(long? total);

        /// <summary>
        /// Sets <see cref="TaskProgressUpdate.Status"/>, keeping the current completed count and total.
        /// </summary>
        /// <param name="status">A short description of the current step, or <see langword="null"/> to clear it.</param>
        void SetStatus(string? status);

        /// <summary>
        /// Sets a callback that computes <see cref="TaskProgressUpdate.Status"/> only when the engine is about to show it.
        /// </summary>
        /// <param name="provider">
        /// The callback, or <see langword="null"/> to remove the current callback and return to the status set by
        /// <see cref="IProgress{T}.Report"/> or <see cref="SetStatus"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// Use this when the status is derived from state that changes often, for example counts per phase. Formatting
        /// the text on every change wastes work, because the engine forwards at most a few updates per second.
        /// </para>
        /// <para>
        /// While a callback is set, it supplies the status for every forwarded update and for the final record, unless a
        /// terminal call supplies a summary. The engine calls it on a thread-pool thread, about once per forwarding interval,
        /// and once more at the terminal call. It must be thread-safe and fast, and it must not block. If it throws,
        /// the engine removes it and uses the explicit status again.
        /// </para>
        /// <para>
        /// For a task that runs in a separate AppDomain, the callback crosses the AppDomain boundary. It must then be
        /// serializable or target a <see cref="MarshalByRefObject"/>.
        /// </para>
        /// </remarks>
        void SetStatusProvider(Func<string?>? provider);

        /// <summary>
        /// Creates a reporter for an operation that runs as part of this operation.
        /// </summary>
        /// <param name="title">A short, stable description of the nested operation.</param>
        /// <param name="unit">The unit used by progress updates of the nested operation.</param>
        /// <param name="retention">
        /// What a live display does with the nested operation after it ends: remove it, or keep its final state
        /// until this operation ends.
        /// </param>
        /// <returns>
        /// A reporter for the nested operation. If this operation already ended, a reporter that ignores every call.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Use a nested operation for a separate measure of progress that belongs to this operation, for example the
        /// packages that a restore downloads while it processes projects. Live displays show the nested operation
        /// below this operation.
        /// </para>
        /// <para>
        /// The nested reporter has the same contract as this reporter, and it has no knowledge of this reporter. The
        /// nested operation ends independently. When this operation ends, every nested operation that is still active
        /// is abandoned first.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="title"/> is <see langword="null"/>, empty, or white space.</exception>
        ITaskProgressReporter CreateNestedReporter(
            string title,
            TaskProgressUnit unit = TaskProgressUnit.Unspecified,
            TaskProgressNestedRetention retention = TaskProgressNestedRetention.Remove);
    }
}
