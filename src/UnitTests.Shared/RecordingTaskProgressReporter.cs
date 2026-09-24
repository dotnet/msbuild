// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// An <see cref="ITaskProgressReporter"/> that records every update and terminal transition so
    /// tests can assert on the progress a task reported. All members are safe to call concurrently,
    /// because tasks such as <c>Copy</c> report from several threads at once.
    /// </summary>
    public sealed class RecordingTaskProgressReporter : ITaskProgressReporter
    {
        private readonly object _lock = new object();
        private readonly List<TaskProgressUpdate> _updates = new List<TaskProgressUpdate>();
        private long _completed;
        private long? _total;
        private bool _isComplete;
        private bool _isCanceled;
        private bool _isFailed;

        /// <summary>
        /// Gets a snapshot of every update the task reported, in the order they were received.
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
        /// Gets the completed value of the most recent update.
        /// </summary>
        public long Completed
        {
            get
            {
                lock (_lock)
                {
                    return _completed;
                }
            }
        }

        /// <summary>
        /// Gets the total value of the most recent update.
        /// </summary>
        public long? Total
        {
            get
            {
                lock (_lock)
                {
                    return _total;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the operation completed successfully.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                lock (_lock)
                {
                    return _isComplete;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the operation was canceled.
        /// </summary>
        public bool IsCanceled
        {
            get
            {
                lock (_lock)
                {
                    return _isCanceled;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the operation failed.
        /// </summary>
        public bool IsFailed
        {
            get
            {
                lock (_lock)
                {
                    return _isFailed;
                }
            }
        }

        public void Report(TaskProgressUpdate value)
        {
            lock (_lock)
            {
                _updates.Add(value);
                _completed = value.Completed;
                _total = value.Total;
            }
        }

        public void Complete(string? summary = null)
        {
            lock (_lock)
            {
                _isComplete = true;
            }
        }

        public void Cancel(string? summary = null)
        {
            lock (_lock)
            {
                _isCanceled = true;
            }
        }

        public void Fail(string? summary = null)
        {
            lock (_lock)
            {
                _isFailed = true;
            }
        }

        public void Dispose()
        {
        }
    }
}
