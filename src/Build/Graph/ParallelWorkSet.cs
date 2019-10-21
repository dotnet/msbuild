// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Provides deduping of expensive work by a key, or modeling of a set of deduped work that
    /// can be awaited as a unit. Completed results are kept in the collection for reuse.
    /// </summary>
    internal class ParallelWorkSet<TKey, TResult>
    {
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Number of workers to process work items.
        /// </summary>
        private readonly int _degreeOfParallelism;

        private readonly ConcurrentDictionary<TKey, Lazy<TResult>> _inProgressOrCompletedWork;

        private bool _isSchedulingCompleted;

        private long _pendingCount;

        private readonly ConcurrentQueue<Lazy<TResult>> _queue =
            new ConcurrentQueue<Lazy<TResult>>();

        private readonly SemaphoreSlim _semaphore;

        private readonly List<Task> _tasks;

        /// <summary>
        /// Retrieves all completed work items.
        /// </summary>
        internal Dictionary<TKey, TResult> CompletedWork
        {
            get
            {
                var completedWork = new Dictionary<TKey, TResult>(_inProgressOrCompletedWork.Count);

                foreach (KeyValuePair<TKey, Lazy<TResult>> kvp in _inProgressOrCompletedWork)
                {
                    Lazy<TResult> workItem = kvp.Value;

                    if (workItem.IsValueCreated)
                    {
                        completedWork[kvp.Key] = workItem.Value;
                    }
                }

                return completedWork;
            }
        }

        /// <summary>
        /// Checks if the work set has been marked as completed.
        /// </summary>
        internal bool IsCompleted
        {
            get => Volatile.Read(ref _isSchedulingCompleted);
            private set => Volatile.Write(ref _isSchedulingCompleted, value);
        }

        internal ParallelWorkSet(int degreeOfParallelism, IEqualityComparer<TKey> comparer, CancellationToken cancellationToken)
        {
            if (degreeOfParallelism < 0)
            {
                throw new ArgumentException("Degree of parallelism must be a positive integer.");
            }

            _cancellationToken = cancellationToken;
            _degreeOfParallelism = degreeOfParallelism;
            _inProgressOrCompletedWork = new ConcurrentDictionary<TKey, Lazy<TResult>>(comparer);

            // Semaphore count is 0 to ensure that all the tasks are blocked unless new data is scheduled.
            _semaphore = new SemaphoreSlim(0, int.MaxValue);
            _tasks = new List<Task>(degreeOfParallelism);

            for (int i = 0; i < degreeOfParallelism; i++)
            {
                _tasks.Add(CreateProcessorItemTask());
            }
        }

        /// <summary>
        /// Enqueues a work item to the work set.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="workFunc"></param>
        internal void AddWork(TKey key, Func<TResult> workFunc)
        {
            if (IsCompleted)
            {
                throw new InvalidOperationException("Cannot add new work after work set is marked as completed.");
            }

            var workItem = new Lazy<TResult>(workFunc);

            if (!_inProgressOrCompletedWork.TryAdd(key, workItem))
            {
                return;
            }

            Interlocked.Increment(ref _pendingCount);

            // NOTE: Enqueue MUST happen before releasing the semaphore
            // to ensure WaitAsync below never returns when there is not
            // a corresponding item in the queue to be dequeued. The only
            // exception is on completion of all items.
            _queue.Enqueue(workItem);
            _semaphore.Release();
        }

        /// <summary>
        /// Assists processing items until all the items added to the queue are processed, completes the work set, and
        /// propagates any exceptions thrown by workers.
        /// </summary>
        internal void WaitForAllWorkAndComplete()
        {
            if (IsCompleted)
            {
                return;
            }
            while (!_cancellationToken.IsCancellationRequested && Interlocked.Read(ref _pendingCount) > 0)
            {
                ExecuteWorkItem();
            }

            IsCompleted = true;

            // Release one thread that will release all the threads when all the elements are processed.
            _semaphore.Release();
            Task.WhenAll(_tasks.ToArray()).GetAwaiter().GetResult();
        }

        private Task CreateProcessorItemTask()
        {
            return Task.Run(
                async () =>
                {
                    bool shouldStopAllWorkers = false;

                    while (!shouldStopAllWorkers)
                    {
                        await _semaphore.WaitAsync(_cancellationToken);

                        try
                        {
                            ExecuteWorkItem();
                        }
                        finally
                        {
                            shouldStopAllWorkers = Interlocked.Read(ref _pendingCount) == 0 && IsCompleted;

                            if (shouldStopAllWorkers)
                            {
                                // Ensure all tasks are unblocked and can gracefully
                                // finish since there are at most degreeOfParallelism - 1 tasks
                                // waiting at this point
                                _semaphore.Release(_degreeOfParallelism);
                            }
                        }
                    }
                },
                _cancellationToken);
        }

        private void ExecuteWorkItem()
        {
            if (_queue.TryDequeue(out Lazy<TResult> workItem))
            {
                try
                {
                    TResult _ = workItem.Value;
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingCount);
                }
            }
        }
    }
}
