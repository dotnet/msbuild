// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Implements the task-facing progress reporting contract for a single operation.
    /// </summary>
    /// <remarks>
    /// Instances are owned by a <see cref="TaskProgressManager"/> scoped to one task invocation.
    /// <see cref="Report(TaskProgressUpdate)"/> is designed to be called concurrently from worker
    /// threads spawned by the task; it must never block on logging, IPC, or rendering. Forwarding
    /// is decoupled behind a delegate rather than <c>ILoggingService</c> directly so this type can
    /// be shared, unmodified, between the in-process <c>TaskHost</c> (which forwards through the
    /// logging service) and out-of-process task hosts (which forward across the node connection).
    /// </remarks>
    internal sealed class TaskProgressReporter :
#if FEATURE_APPDOMAIN
        MarshalByRefObject,
#endif
        ITaskProgressReporter
    {
        /// <summary>
        /// The minimum interval, in milliseconds, between forwarded intermediate updates for one operation.
        /// Begin and end records always bypass this throttle.
        /// </summary>
        private const long MinimumForwardIntervalMilliseconds = 150;

        private enum ReporterState
        {
            Active = 0,
            Completed = 1,
            Canceled = 2,
            Failed = 3,
            Abandoned = 4,
        }

        private readonly TaskProgressManager _manager;
        private readonly Action<BuildEventArgs>? _logEvent;
        private readonly object _stateLock = new object();
        private int _state = (int)ReporterState.Active;
        private long _sequence;
        private int _lastForwardedTicks;
        private bool _hasForwardedUpdate;
        private TaskProgressUpdate _latestUpdate;

        internal TaskProgressReporter(
            TaskProgressManager manager,
            long operationId,
            string title,
            TaskProgressUnit unit,
            BuildEventContext buildEventContext,
            Action<BuildEventArgs>? logEvent)
        {
            _manager = manager;
            _logEvent = logEvent;
            OperationId = operationId;
            Title = title;
            Unit = unit;
            BuildEventContext = buildEventContext;
            _latestUpdate = new TaskProgressUpdate(0);

            _logEvent?.Invoke(new TaskProgressStartedEventArgs(operationId, title, unit)
            {
                BuildEventContext = buildEventContext,
            });
        }

        /// <summary>
        /// Gets the engine-generated identifier for this operation, unique within the build.
        /// </summary>
        internal long OperationId { get; }

        /// <summary>
        /// Gets the short, stable description supplied when the reporter was created.
        /// </summary>
        internal string Title { get; }

        /// <summary>
        /// Gets the unit that <see cref="TaskProgressUpdate.Completed"/> and
        /// <see cref="TaskProgressUpdate.Total"/> are expressed in.
        /// </summary>
        internal TaskProgressUnit Unit { get; }

        /// <summary>
        /// Gets the build event context the operation was created under.
        /// </summary>
        internal BuildEventContext BuildEventContext { get; }

        /// <summary>
        /// Gets a value indicating whether the reporter has not yet reached a terminal state.
        /// </summary>
        internal bool IsActive => Volatile.Read(ref _state) == (int)ReporterState.Active;

        /// <summary>
        /// Gets the most recently accepted update, or its final value once the reporter has closed.
        /// </summary>
        internal TaskProgressUpdate LatestUpdate
        {
            get
            {
                lock (_stateLock)
                {
                    return _latestUpdate;
                }
            }
        }

        /// <summary>
        /// Gets the number of accepted state transitions, including terminal transitions.
        /// </summary>
        /// <remarks>
        /// Lets a future forwarding layer discard stale packets without interpreting task values.
        /// </remarks>
        internal long Sequence
        {
            get
            {
                lock (_stateLock)
                {
                    return _sequence;
                }
            }
        }

        /// <inheritdoc/>
        public void Report(TaskProgressUpdate value)
        {
            if (!IsActive)
            {
                return;
            }

            bool shouldForward = false;
            long sequence = 0;

            lock (_stateLock)
            {
                if (_state != (int)ReporterState.Active)
                {
                    return;
                }

                _latestUpdate = value;
                sequence = ++_sequence;

                if (_logEvent != null)
                {
                    int nowTicks = Environment.TickCount;
                    if (!_hasForwardedUpdate || unchecked(nowTicks - _lastForwardedTicks) >= MinimumForwardIntervalMilliseconds)
                    {
                        _lastForwardedTicks = nowTicks;
                        _hasForwardedUpdate = true;
                        shouldForward = true;
                    }
                }
            }

            if (shouldForward)
            {
                _logEvent!.Invoke(new TaskProgressUpdatedEventArgs(OperationId, sequence, value.Completed, value.Total, value.Status)
                {
                    BuildEventContext = BuildEventContext,
                });
            }
        }

        /// <inheritdoc/>
        public void Complete(string? summary = null) => TryTransition(ReporterState.Completed, summary);

        /// <inheritdoc/>
        public void Cancel(string? summary = null) => TryTransition(ReporterState.Canceled, summary);

        /// <inheritdoc/>
        public void Fail(string? summary = null) => TryTransition(ReporterState.Failed, summary);

        /// <inheritdoc/>
        public void Dispose() => TryTransition(ReporterState.Abandoned, null);

        /// <summary>
        /// Closes the reporter as abandoned because the owning task finished without an explicit
        /// terminal call. Safe to call after an explicit terminal transition; it becomes a no-op.
        /// </summary>
        internal void Abandon() => TryTransition(ReporterState.Abandoned, null);

        private void TryTransition(ReporterState target, string? summary)
        {
            bool transitioned;
            long sequence = 0;
            TaskProgressUpdate finalUpdate = default;

            lock (_stateLock)
            {
                transitioned = _state == (int)ReporterState.Active;

                if (transitioned)
                {
                    if (summary != null)
                    {
                        _latestUpdate = new TaskProgressUpdate(_latestUpdate.Completed, _latestUpdate.Total, summary);
                    }

                    sequence = ++_sequence;
                    finalUpdate = _latestUpdate;
                    _state = (int)target;
                }
            }

            if (transitioned)
            {
                _logEvent?.Invoke(new TaskProgressFinishedEventArgs(
                    OperationId,
                    sequence,
                    ToOutcome(target),
                    finalUpdate.Completed,
                    finalUpdate.Total,
                    finalUpdate.Status)
                {
                    BuildEventContext = BuildEventContext,
                });

                _manager.OnReporterClosed(this);
            }
        }

        private static TaskProgressOutcome ToOutcome(ReporterState state) => state switch
        {
            ReporterState.Completed => TaskProgressOutcome.Completed,
            ReporterState.Canceled => TaskProgressOutcome.Canceled,
            ReporterState.Failed => TaskProgressOutcome.Failed,
            _ => TaskProgressOutcome.Abandoned,
        };
    }
}
