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
        private readonly object _stateLock = new();
        private readonly object _eventLock = new();
        private readonly TaskProgressStartedEventArgs _startedEvent;
        private int _state = (int)ReporterState.Active;
        private long _sequence;
        private int _lastForwardedTicks;
        private bool _hasForwardedUpdate;
        private TaskProgressUpdate _lastForwardedUpdate;
        private bool _flushScheduled;
        private Timer? _flushTimer;
        private TaskProgressUpdate _latestUpdate;
        private long _lastForwardedSequence;
        private Func<string?>? _statusProvider;
        private int _polling;
        private List<TaskProgressReporter>? _nestedReporters;
        private bool _started;
        // Teardown can race the start callback after this reporter has been registered with its manager.
        private BuildEventArgs? _pendingTerminalEvent;

        internal TaskProgressReporter(
            TaskProgressManager manager,
            long operationId,
            string title,
            TaskProgressUnit unit,
            BuildEventContext buildEventContext,
            Action<BuildEventArgs>? logEvent,
            long parentOperationId,
            TaskProgressNestedRetention retention)
        {
            _manager = manager;
            _logEvent = logEvent;
            OperationId = operationId;
            Title = title;
            Unit = unit;
            BuildEventContext = buildEventContext;
            _latestUpdate = new TaskProgressUpdate(0);
            _startedEvent = new TaskProgressStartedEventArgs(OperationId, Title, Unit)
            {
                BuildEventContext = BuildEventContext,
                ParentOperationId = parentOperationId,
                Retention = retention,
            };
        }

        internal void Start()
        {
            LogEvent(_startedEvent);

            BuildEventArgs? pendingTerminalEvent;
            lock (_eventLock)
            {
                _started = true;
                pendingTerminalEvent = _pendingTerminalEvent;
                _pendingTerminalEvent = null;
            }

            if (pendingTerminalEvent is not null)
            {
                LogEvent(pendingTerminalEvent);
            }
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
        /// Gets the number of accepted state transitions, including terminal transitions and forwarded
        /// changes that come only from the status provider.
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
        public void Report(TaskProgressUpdate value) => Apply(Change.Replace, value, 0);

        /// <inheritdoc/>
        public ITaskProgressReporter CreateNestedReporter(
            string title,
            TaskProgressUnit unit = TaskProgressUnit.Unspecified,
            TaskProgressNestedRetention retention = TaskProgressNestedRetention.Remove)
        {
            TaskProgressManager.ValidateTitle(title);

            // The nested reporter is created under the lock, so its begin record is always forwarded after this
            // operation's begin record and before this operation's end record. Nested operations are rare, so
            // forwarding one record under the lock is acceptable.
            lock (_stateLock)
            {
                if (_state != (int)ReporterState.Active)
                {
                    return EngineServices.NullTaskProgressReporter.Instance;
                }

                _nestedReporters ??= [];
                _nestedReporters.RemoveAll(static nested => !nested.IsActive);

                ITaskProgressReporter reporter = _manager.CreateNestedReporter(title, unit, BuildEventContext, _logEvent, OperationId, retention);
                if (reporter is TaskProgressReporter nestedReporter)
                {
                    _nestedReporters.Add(nestedReporter);
                }

                return reporter;
            }
        }

        /// <inheritdoc/>
        public void Increment(long delta = 1) => Apply(Change.Increment, default, delta);

        /// <inheritdoc/>
        public void AddToTotal(long delta) => Apply(Change.AddToTotal, default, delta);

        /// <inheritdoc/>
        public void SetTotal(long? total) => Apply(Change.SetTotal, new TaskProgressUpdate(0, total), 0);

        /// <inheritdoc/>
        public void SetStatus(string? status) => Apply(Change.SetStatus, new TaskProgressUpdate(0, null, status), 0);

        /// <inheritdoc/>
        public void SetStatusProvider(Func<string?>? provider)
        {
            bool shouldForward = false;
            long sequence = 0;
            TaskProgressUpdate update = default;

            lock (_stateLock)
            {
                if (_state != (int)ReporterState.Active || ReferenceEquals(provider, _statusProvider))
                {
                    return;
                }

                _statusProvider = provider;
                if (_logEvent is null)
                {
                    return;
                }

                if (provider is not null)
                {
                    // Poll the provider once per forwarding interval. Its value can change without any call
                    // on the reporter, so forwarding is timer driven while a provider is set.
                    _flushScheduled = false;
                    long dueTime = _hasForwardedUpdate
                        ? Math.Max(0, MinimumForwardIntervalMilliseconds - unchecked(Environment.TickCount - _lastForwardedTicks))
                        : 0;
                    EnsureTimer().Change(dueTime, MinimumForwardIntervalMilliseconds);
                }
                else
                {
                    // Return to event-driven forwarding. The explicit status replaces the last provider value.
                    _flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    shouldForward = TryClaimForward(out sequence);
                    update = _latestUpdate;
                }
            }

            if (shouldForward)
            {
                Forward(sequence, update);
            }
        }

        private enum Change
        {
            Replace,
            Increment,
            AddToTotal,
            SetTotal,
            SetStatus,
        }

        /// <summary>
        /// Applies one change to the latest accepted update atomically, so concurrent callers never lose
        /// each other's increments, and forwards the result subject to deduplication and the throttle.
        /// </summary>
        private void Apply(Change change, TaskProgressUpdate value, long delta)
        {
            if (!IsActive)
            {
                return;
            }

            bool shouldForward;
            long sequence = 0;
            TaskProgressUpdate update;

            lock (_stateLock)
            {
                if (_state != (int)ReporterState.Active)
                {
                    return;
                }

                TaskProgressUpdate current = _latestUpdate;
                update = change switch
                {
                    Change.Increment => new TaskProgressUpdate(ClampedAdd(current.Completed, delta), current.Total, current.Status),
                    Change.AddToTotal => new TaskProgressUpdate(current.Completed, ClampedAdd(current.Total ?? 0, delta), current.Status),
                    Change.SetTotal => new TaskProgressUpdate(current.Completed, value.Total is long total ? Math.Max(total, 0) : null, current.Status),
                    Change.SetStatus => new TaskProgressUpdate(current.Completed, current.Total, value.Status),
                    _ => value,
                };

                // An update identical to the latest accepted one carries no information. Dropping it here
                // means adopters do not need their own last-reported bookkeeping to avoid duplicate frames.
                if (IsSameUpdate(update, current))
                {
                    return;
                }

                _latestUpdate = update;
                ++_sequence;

                // While a status provider is set, the polling timer forwards the change.
                shouldForward = _statusProvider is null && TryClaimForward(out sequence);
            }

            if (shouldForward)
            {
                Forward(sequence, update);
            }
        }

        /// <summary>
        /// Adds <paramref name="delta"/> to a counter, saturating at <see cref="long.MaxValue"/> and never going below zero,
        /// because reporter members must not throw.
        /// </summary>
        private static long ClampedAdd(long value, long delta)
        {
            long result = unchecked(value + delta);
            if (delta > 0 && result < value)
            {
                return long.MaxValue;
            }

            if (delta < 0 && result > value)
            {
                return 0;
            }

            return Math.Max(result, 0);
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

        /// <summary>
        /// Forwards the latest update that was held back by the throttle, if it was not already
        /// superseded by a forwarded update or a terminal transition.
        /// </summary>
        private void FlushPendingUpdate()
        {
            bool shouldForward;
            long sequence;
            TaskProgressUpdate update;

            lock (_stateLock)
            {
                if (!_flushScheduled || _state != (int)ReporterState.Active)
                {
                    return;
                }

                _flushScheduled = false;
                shouldForward = TryClaimForward(out sequence);
                update = _latestUpdate;
            }

            // A terminal transition can race ahead of this invocation. Its sequence number is higher,
            // so consumers that order by sequence discard this update.
            if (shouldForward)
            {
                Forward(sequence, update);
            }
        }

        /// <summary>
        /// Decides whether the latest accepted update is forwarded now, later on the trailing edge of the
        /// throttle window, or not at all because it matches what was already forwarded.
        /// </summary>
        /// <remarks>The caller must hold <see cref="_stateLock"/>.</remarks>
        private bool TryClaimForward(out long sequence)
        {
            sequence = 0;
            if (_logEvent is null)
            {
                return false;
            }

            if (_hasForwardedUpdate && IsSameUpdate(_latestUpdate, _lastForwardedUpdate))
            {
                _flushScheduled = false;
                return false;
            }

            int nowTicks = Environment.TickCount;
            int elapsed = unchecked(nowTicks - _lastForwardedTicks);
            if (_hasForwardedUpdate && elapsed < MinimumForwardIntervalMilliseconds)
            {
                ScheduleFlush(MinimumForwardIntervalMilliseconds - elapsed);
                return false;
            }

            _lastForwardedTicks = nowTicks;
            _hasForwardedUpdate = true;
            _lastForwardedUpdate = _latestUpdate;
            _flushScheduled = false;
            sequence = ClaimSequence();
            return true;
        }

        /// <summary>
        /// Returns the sequence number for a record about to be forwarded. Forwarded sequence numbers strictly
        /// increase, even when the record differs from the previous one only by a status provider value.
        /// </summary>
        /// <remarks>The caller must hold <see cref="_stateLock"/>.</remarks>
        private long ClaimSequence()
        {
            if (_sequence <= _lastForwardedSequence)
            {
                ++_sequence;
            }

            _lastForwardedSequence = _sequence;
            return _sequence;
        }

        /// <summary>
        /// Evaluates the status provider on behalf of the forwarding path and forwards the result when it changes
        /// the visible state. Runs on the thread pool once per forwarding interval while a provider is set.
        /// </summary>
        private void PollStatusProvider()
        {
            // Timer callbacks can overlap when the provider is slow; one evaluation at a time is enough.
            if (Interlocked.Exchange(ref _polling, 1) == 1)
            {
                return;
            }

            try
            {
                Func<string?>? provider = Volatile.Read(ref _statusProvider);
                if (provider is null || !IsActive)
                {
                    return;
                }

                // The provider is task code, so it runs outside the lock.
                if (!TryEvaluateStatusProvider(provider, out string? status))
                {
                    RemoveFailedStatusProvider(provider);
                    return;
                }

                long sequence;
                TaskProgressUpdate update;
                lock (_stateLock)
                {
                    if (_state != (int)ReporterState.Active || !ReferenceEquals(_statusProvider, provider))
                    {
                        return;
                    }

                    update = new TaskProgressUpdate(_latestUpdate.Completed, _latestUpdate.Total, status);
                    if (IsSameUpdate(update, _lastForwardedUpdate))
                    {
                        return;
                    }

                    _lastForwardedTicks = Environment.TickCount;
                    _hasForwardedUpdate = true;
                    _lastForwardedUpdate = update;
                    sequence = ClaimSequence();
                }

                Forward(sequence, update);
            }
            finally
            {
                Volatile.Write(ref _polling, 0);
            }
        }

        private void RemoveFailedStatusProvider(Func<string?> provider)
        {
            bool shouldForward;
            long sequence;
            TaskProgressUpdate update;

            lock (_stateLock)
            {
                if (_state != (int)ReporterState.Active || !ReferenceEquals(_statusProvider, provider))
                {
                    return;
                }

                // A failing provider falls back to the explicit status rather than failing the task.
                _statusProvider = null;
                _flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                shouldForward = TryClaimForward(out sequence);
                update = _latestUpdate;
            }

            if (shouldForward)
            {
                Forward(sequence, update);
            }
        }

        private static bool TryEvaluateStatusProvider(Func<string?> provider, out string? status)
        {
            try
            {
                status = provider();
                return true;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                status = null;
                return false;
            }
        }

        private void OnTimer()
        {
            try
            {
                if (Volatile.Read(ref _statusProvider) is not null)
                {
                    PollStatusProvider();
                }
                else
                {
                    FlushPendingUpdate();
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // An exception escaping a thread-pool callback would terminate the process.
            }
        }

        /// <remarks>The caller must hold <see cref="_stateLock"/>.</remarks>
        private Timer EnsureTimer()
            => _flushTimer ??= new Timer(static state => ((TaskProgressReporter)state!).OnTimer(), this, Timeout.Infinite, Timeout.Infinite);

        /// <remarks>The caller must hold <see cref="_stateLock"/>.</remarks>
        private void ScheduleFlush(long dueTime)
        {
            if (_flushScheduled)
            {
                return;
            }

            // Trailing edge: the throttled value must still be forwarded once the window closes,
            // otherwise the last state before a pause stays invisible until the end.
            _flushScheduled = true;
            EnsureTimer().Change(dueTime, Timeout.Infinite);
        }

        private void Forward(long sequence, TaskProgressUpdate update)
            => TryLogEvent(new TaskProgressUpdatedEventArgs(OperationId, sequence, update.Completed, update.Total, update.Status)
            {
                BuildEventContext = BuildEventContext,
            });

        /// <summary>
        /// Forwards a progress record. Progress is presentation data, so a failure to deliver it must
        /// never surface to the task: every public member of this type is documented as non-throwing,
        /// and the trailing flush runs on a thread-pool thread where an unhandled exception would
        /// terminate the process.
        /// </summary>
        private void TryLogEvent(BuildEventArgs e)
        {
            lock (_eventLock)
            {
                if (!_started)
                {
                    _pendingTerminalEvent = e;
                    return;
                }
            }

            LogEvent(e);
        }

        private void LogEvent(BuildEventArgs e)
        {
            try
            {
                _logEvent?.Invoke(e);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
            }
        }

        private static bool IsSameUpdate(TaskProgressUpdate left, TaskProgressUpdate right)
            => left.Completed == right.Completed
                && left.Total == right.Total
                && string.Equals(left.Status, right.Status, StringComparison.Ordinal);

        private void TryTransition(ReporterState target, string? summary)
        {
            long sequence;
            TaskProgressUpdate finalUpdate;
            List<TaskProgressReporter>? nestedReporters;

            // The final record carries the provider's latest value unless the task supplied a summary.
            // The provider is task code, so it runs before the lock is taken.
            Func<string?>? provider = summary is null && IsActive ? Volatile.Read(ref _statusProvider) : null;
            bool hasProviderStatus = false;
            string? providerStatus = null;
            if (provider is not null)
            {
                hasProviderStatus = TryEvaluateStatusProvider(provider, out providerStatus);
            }

            lock (_stateLock)
            {
                if (_state != (int)ReporterState.Active)
                {
                    return;
                }

                _flushScheduled = false;
                _flushTimer?.Dispose();
                _flushTimer = null;

                if (summary is not null)
                {
                    _latestUpdate = new TaskProgressUpdate(_latestUpdate.Completed, _latestUpdate.Total, summary);
                }
                else if (hasProviderStatus && ReferenceEquals(_statusProvider, provider))
                {
                    _latestUpdate = new TaskProgressUpdate(_latestUpdate.Completed, _latestUpdate.Total, providerStatus);
                }

                _statusProvider = null;
                sequence = ++_sequence;
                finalUpdate = _latestUpdate;
                _state = (int)target;
                nestedReporters = _nestedReporters;
                _nestedReporters = null;
            }

            // A nested operation cannot outlive its parent. End records of nested operations come first, so
            // loggers never see a nested operation whose parent already ended.
            if (nestedReporters is not null)
            {
                foreach (TaskProgressReporter nested in nestedReporters)
                {
                    nested.Abandon();
                }
            }

            TryLogEvent(new TaskProgressFinishedEventArgs(
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

        private static TaskProgressOutcome ToOutcome(ReporterState state) => state switch
        {
            ReporterState.Completed => TaskProgressOutcome.Completed,
            ReporterState.Canceled => TaskProgressOutcome.Canceled,
            ReporterState.Failed => TaskProgressOutcome.Failed,
            _ => TaskProgressOutcome.Abandoned,
        };
    }
}
