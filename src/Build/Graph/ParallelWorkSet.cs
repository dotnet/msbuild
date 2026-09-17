// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

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

        private readonly ConcurrentDictionary<TKey, WorkItem> _inProgressOrCompletedWork;

        private bool _isSchedulingCompleted;

        private long _pendingCount;

        private readonly ConcurrentQueue<WorkItem> _queue =
            new ConcurrentQueue<WorkItem>();

        private readonly SemaphoreSlim _semaphore;

        private readonly List<Task> _tasks;

        private readonly List<Exception> _exceptions = new List<Exception>(0);

        /// <summary>
        /// Retrieves all completed work items.
        /// </summary>
        internal Dictionary<TKey, TResult> CompletedWork
        {
            get
            {
                var completedWork = new Dictionary<TKey, TResult>(_inProgressOrCompletedWork.Count);

                foreach (KeyValuePair<TKey, WorkItem> kvp in _inProgressOrCompletedWork)
                {
                    WorkItem workItem = kvp.Value;

                    if (workItem.IsValueCreated)
                    {
                        completedWork[kvp.Key] = workItem.Value;
                    }
                }

                if (_exceptions.Count > 0)
                {
                    throw new AggregateException(_exceptions);
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
            _inProgressOrCompletedWork = new ConcurrentDictionary<TKey, WorkItem>(comparer);

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

            WorkItem workItem = new(workFunc);

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
            Task.WaitAll(
#if NET
                _tasks);
#else
                _tasks.ToArray());
#endif

            if (_exceptions.Count > 0)
            {
                throw new AggregateException(_exceptions);
            }
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
            if (_queue.TryDequeue(out WorkItem workItem))
            {
                try
                {
                    _ = workItem.Value;
                }
                catch (Exception ex)
                {
                    lock (_exceptions)
                    {
                        _exceptions.Add(ex);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingCount);
                }
            }
        }

        /// <summary>
        /// A lazily-evaluated work item. Unlike <see cref="Lazy{T}"/>, this does not place a
        /// <c>DynamicallyAccessedMembers</c> requirement on <typeparamref name="TResult"/> (<see cref="Lazy{T}"/>
        /// annotates its type argument for the parameterless constructor, which triggers IL2091 here), and it is
        /// only ever evaluated once by a single worker before the result is read after
        /// <see cref="WaitForAllWorkAndComplete"/> establishes a happens-before barrier.
        /// </summary>
        private sealed class WorkItem
        {
            private Func<TResult> _workFunc;
            private bool _isValueCreated;
            private TResult _value;

            internal WorkItem(Func<TResult> workFunc)
            {
                _workFunc = workFunc;
            }

            internal bool IsValueCreated => _isValueCreated;

            internal TResult Value
            {
                get
                {
                    if (!_isValueCreated)
                    {
                        _value = _workFunc();
                        _isValueCreated = true;

                        // Release the factory (and anything its closure captured) once the value is
                        // computed. Completed work items are retained in the set for reuse, so holding
                        // the delegate would keep captured graph state alive for the whole build.
                        // Matches Lazy<T>, which also drops its value factory after evaluation.
                        _workFunc = null;
                    }

                    return _value;
                }
            }
        }
    }
}
