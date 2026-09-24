// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Owns all <see cref="TaskProgressReporter"/> instances created during one task invocation.
    /// </summary>
    /// <remarks>
    /// A new instance is scoped to a single task-hosting context, whether that is an in-process
    /// <c>TaskHost</c> or a per-task context in an out-of-process task host. This type is compiled
    /// into both <c>Microsoft.Build</c> and <c>MSBuild</c> (see <c>src/Shared</c>) so both hosting
    /// mechanisms can share the same lifecycle guarantees. The manager assigns engine-owned
    /// operation identifiers so that tasks cannot choose or collide with another operation's
    /// identity, and it guarantees that every reporter it created reaches a terminal state, even
    /// if the task leaks one.
    /// </remarks>
    internal sealed class TaskProgressManager
    {
        /// <summary>
        /// Generates operation identifiers that are unique within the current process.
        /// </summary>
        /// <remarks>
        /// Distributed builds will need a node-qualified identifier once out-of-process transport
        /// is implemented; this counter is sufficient for the initial single-process slice.
        /// </remarks>
        private static long s_nextOperationId;

        private readonly object _lock = new object();
        private readonly List<TaskProgressReporter> _activeReporters = new List<TaskProgressReporter>();

        /// <summary>
        /// Creates a new reporter for one operation, correlated to the supplied build event context.
        /// </summary>
        /// <param name="title">A short, stable description of the operation. Must not be null, empty, or whitespace.</param>
        /// <param name="unit">The unit that progress values are expressed in.</param>
        /// <param name="buildEventContext">The build event context to correlate the operation with.</param>
        /// <param name="logEvent">
        /// The callback used to forward begin, update, and end records, or <see langword="null"/> if
        /// this task invocation has no active logging sink (in which case progress is tracked but never forwarded).
        /// In-process hosts forward to <c>ILoggingService.LogBuildEvent</c>; out-of-process task hosts forward
        /// across the node connection instead.
        /// </param>
        internal ITaskProgressReporter CreateReporter(string title, TaskProgressUnit unit, BuildEventContext buildEventContext, Action<BuildEventArgs>? logEvent = null)
        {
            if (title is null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Value cannot be empty or consist only of white-space.", nameof(title));
            }

            long operationId = Interlocked.Increment(ref s_nextOperationId);
            var reporter = new TaskProgressReporter(this, operationId, title, unit, buildEventContext, logEvent);

            lock (_lock)
            {
                _activeReporters.Add(reporter);
            }

            return reporter;
        }

        /// <summary>
        /// Removes a reporter from the active set once it has reached a terminal state.
        /// </summary>
        internal void OnReporterClosed(TaskProgressReporter reporter)
        {
            lock (_lock)
            {
                _activeReporters.Remove(reporter);
            }
        }

        /// <summary>
        /// Abandons every reporter that the owning task did not close before it finished executing.
        /// </summary>
        internal void AbandonRemaining()
        {
            TaskProgressReporter[] remaining;

            lock (_lock)
            {
                if (_activeReporters.Count == 0)
                {
                    return;
                }

                remaining = _activeReporters.ToArray();
                _activeReporters.Clear();
            }

            foreach (TaskProgressReporter reporter in remaining)
            {
                reporter.Abandon();
            }
        }
    }
}
