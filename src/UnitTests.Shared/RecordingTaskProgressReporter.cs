// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// An <see cref="ITaskProgressReporter"/> that records what a task reported so tests can assert on it.
    /// </summary>
    /// <remarks>
    /// The recorder follows the engine contract: identical updates are not recorded, the first outcome wins,
    /// calls after an outcome are ignored, disposal without an outcome records <see cref="TaskProgressOutcome.Abandoned"/>,
    /// and no member throws. It does not throttle, so every distinct change is recorded. All members are safe to call
    /// concurrently, because tasks such as <c>Copy</c> report from several threads at once.
    /// </remarks>
    public sealed class RecordingTaskProgressReporter : ITaskProgressReporter
    {
        private readonly object _lock = new object();
        private readonly List<TaskProgressUpdate> _updates = new List<TaskProgressUpdate>();
        private readonly List<RecordingTaskProgressReporter> _nestedReporters = new List<RecordingTaskProgressReporter>();
        private TaskProgressUpdate _latest = new TaskProgressUpdate(0);
        private Func<string?>? _statusProvider;
        private TaskProgressOutcome? _outcome;
        private string? _summary;

        /// <summary>
        /// Creates a recorder that is not associated with a title, for example to assign to <see cref="MockEngine.TaskProgressReporter"/>.
        /// </summary>
        public RecordingTaskProgressReporter()
        {
        }

        /// <summary>
        /// Creates a recorder for an operation with the given title and unit.
        /// </summary>
        public RecordingTaskProgressReporter(string title, TaskProgressUnit unit)
        {
            Title = title;
            Unit = unit;
        }

        /// <summary>
        /// Gets the title of the operation, or <see langword="null"/> if the recorder was not created through an engine.
        /// </summary>
        public string? Title { get; internal set; }

        /// <summary>
        /// Gets the unit of the operation.
        /// </summary>
        public TaskProgressUnit Unit { get; internal set; }

        /// <summary>
        /// Gets a snapshot of every distinct update the task reported, in the order they were accepted.
        /// </summary>
        public IReadOnlyList<TaskProgressUpdate> Updates
        {
            get
            {
                lock (_lock)
                {
                    return _updates.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the latest accepted update.
        /// </summary>
        public TaskProgressUpdate LatestUpdate
        {
            get
            {
                lock (_lock)
                {
                    return _latest;
                }
            }
        }

        /// <summary>
        /// Gets the completed value of the latest accepted update.
        /// </summary>
        public long Completed => LatestUpdate.Completed;

        /// <summary>
        /// Gets the total value of the latest accepted update.
        /// </summary>
        public long? Total => LatestUpdate.Total;

        /// <summary>
        /// Gets the status the engine would show now: the value of the status provider if one is set,
        /// otherwise the status of the latest accepted update.
        /// </summary>
        public string? Status
        {
            get
            {
                Func<string?>? provider;
                string? status;
                lock (_lock)
                {
                    provider = _statusProvider;
                    status = _latest.Status;
                }

                return provider is not null && TryEvaluate(provider, out string? providerStatus) ? providerStatus : status;
            }
        }

        /// <summary>
        /// Gets the status provider the task set, or <see langword="null"/> if none is set.
        /// </summary>
        public Func<string?>? StatusProvider
        {
            get
            {
                lock (_lock)
                {
                    return _statusProvider;
                }
            }
        }

        /// <summary>
        /// Gets how the operation ended, or <see langword="null"/> while it is still active.
        /// </summary>
        public TaskProgressOutcome? Outcome
        {
            get
            {
                lock (_lock)
                {
                    return _outcome;
                }
            }
        }

        /// <summary>
        /// Gets the status of the final record: the summary passed to the outcome call, otherwise the status the engine
        /// would have shown at that moment.
        /// </summary>
        public string? Summary
        {
            get
            {
                lock (_lock)
                {
                    return _summary;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the operation completed successfully.
        /// </summary>
        public bool IsComplete => Outcome == TaskProgressOutcome.Completed;

        /// <summary>
        /// Gets a value indicating whether the operation was canceled.
        /// </summary>
        public bool IsCanceled => Outcome == TaskProgressOutcome.Canceled;

        /// <summary>
        /// Gets a value indicating whether the operation failed.
        /// </summary>
        public bool IsFailed => Outcome == TaskProgressOutcome.Failed;

        /// <summary>
        /// Gets a value indicating whether the reporter was disposed without an outcome.
        /// </summary>
        public bool IsAbandoned => Outcome == TaskProgressOutcome.Abandoned;

        public void Report(TaskProgressUpdate value) => Apply(_ => value);

        public void Increment(long delta = 1)
            => Apply(current => new TaskProgressUpdate(ClampedAdd(current.Completed, delta), current.Total, current.Status));

        public void AddToTotal(long delta)
            => Apply(current => new TaskProgressUpdate(current.Completed, ClampedAdd(current.Total ?? 0, delta), current.Status));

        public void SetTotal(long? total)
            => Apply(current => new TaskProgressUpdate(current.Completed, total is long value ? Math.Max(value, 0) : null, current.Status));

        public void SetStatus(string? status)
            => Apply(current => new TaskProgressUpdate(current.Completed, current.Total, status));

        public void SetStatusProvider(Func<string?>? provider)
        {
            lock (_lock)
            {
                if (_outcome is null)
                {
                    _statusProvider = provider;
                }
            }
        }

        public void Complete(string? summary = null) => End(TaskProgressOutcome.Completed, summary);

        public void Cancel(string? summary = null) => End(TaskProgressOutcome.Canceled, summary);

        public void Fail(string? summary = null) => End(TaskProgressOutcome.Failed, summary);

        public void Dispose() => End(TaskProgressOutcome.Abandoned, null);

        /// <summary>
        /// Gets the retention the parent chose when it created this recorder as a nested reporter.
        /// </summary>
        public TaskProgressNestedRetention Retention { get; private set; }

        /// <summary>
        /// Gets a snapshot of the nested reporters created while the operation was active, in creation order.
        /// </summary>
        public IReadOnlyList<RecordingTaskProgressReporter> NestedReporters
        {
            get
            {
                lock (_lock)
                {
                    return _nestedReporters.ToArray();
                }
            }
        }

        public ITaskProgressReporter CreateNestedReporter(
            string title,
            TaskProgressUnit unit = TaskProgressUnit.Unspecified,
            TaskProgressNestedRetention retention = TaskProgressNestedRetention.Remove)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Value cannot be null, empty, or consist only of white-space.", nameof(title));
            }

            lock (_lock)
            {
                if (_outcome is null)
                {
                    var nested = new RecordingTaskProgressReporter(title, unit) { Retention = retention };
                    _nestedReporters.Add(nested);
                    return nested;
                }
            }

            // Like the engine, a reporter that already ended hands out a reporter that ignores every call.
            var ended = new RecordingTaskProgressReporter(title, unit) { Retention = retention };
            ended.Dispose();
            return ended;
        }

        private void Apply(Func<TaskProgressUpdate, TaskProgressUpdate> change)
        {
            lock (_lock)
            {
                if (_outcome is not null)
                {
                    return;
                }

                TaskProgressUpdate update = change(_latest);
                if (update.Completed == _latest.Completed
                    && update.Total == _latest.Total
                    && string.Equals(update.Status, _latest.Status, StringComparison.Ordinal))
                {
                    return;
                }

                _latest = update;
                _updates.Add(update);
            }
        }

        private void End(TaskProgressOutcome outcome, string? summary)
        {
            Func<string?>? provider;
            lock (_lock)
            {
                if (_outcome is not null)
                {
                    return;
                }

                provider = summary is null ? _statusProvider : null;
            }

            // Like the engine, evaluate the provider outside the lock and fall back to the explicit status if it throws.
            string? providerStatus = null;
            bool hasProviderStatus = provider is not null && TryEvaluate(provider, out providerStatus);

            RecordingTaskProgressReporter[] nestedReporters;
            lock (_lock)
            {
                if (_outcome is not null)
                {
                    return;
                }

                _outcome = outcome;
                _summary = summary ?? (hasProviderStatus ? providerStatus : _latest.Status);
                _statusProvider = null;
                nestedReporters = _nestedReporters.ToArray();
            }

            // Like the engine, nested operations that are still active are abandoned when the parent ends.
            foreach (RecordingTaskProgressReporter nested in nestedReporters)
            {
                nested.Dispose();
            }
        }

        private static bool TryEvaluate(Func<string?> provider, out string? status)
        {
            try
            {
                status = provider();
                return true;
            }
            catch
            {
                status = null;
                return false;
            }
        }

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
    }
}
