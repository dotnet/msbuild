// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Class defining extension methods for awaitable objects.
    /// </summary>
    internal static class AwaitExtensions
    {
        /// <summary>
        /// Synchronizes access to the staScheduler field.
        /// </summary>
        private static Object s_staSchedulerSync = new Object();

        /// <summary>
        /// The singleton STA scheduler object.
        /// </summary>
        private static TaskScheduler s_staScheduler;

        /// <summary>
        /// Gets the STA scheduler.
        /// </summary>
        internal static TaskScheduler OneSTAThreadPerTaskSchedulerInstance
        {
            get
            {
                if (s_staScheduler == null)
                {
                    lock (s_staSchedulerSync)
                    {
                        if (s_staScheduler == null)
                        {
                            s_staScheduler = new OneSTAThreadPerTaskScheduler();
                        }
                    }
                }

                return s_staScheduler;
            }
        }

        /// <summary>
        /// Provides await functionality for ordinary <see cref="WaitHandle"/>s.
        /// </summary>
        /// <param name="handle">The handle to wait on.</param>
        /// <returns>The awaiter.</returns>
        internal static TaskAwaiter GetAwaiter(this WaitHandle handle)
        {
            ErrorUtilities.VerifyThrowArgumentNull(handle, "handle");
            return handle.ToTask().GetAwaiter();
        }

        /// <summary>
        /// Provides await functionality for an array of ordinary <see cref="WaitHandle"/>s.
        /// </summary>
        /// <param name="handles">The handles to wait on.</param>
        /// <returns>The awaiter.</returns>
        internal static TaskAwaiter<int> GetAwaiter(this WaitHandle[] handles)
        {
            ErrorUtilities.VerifyThrowArgumentNull(handles, "handle");
            return handles.ToTask().GetAwaiter();
        }

        /// <summary>
        /// Creates a TPL Task that is marked as completed when a <see cref="WaitHandle"/> is signaled.
        /// </summary>
        /// <param name="handle">The handle whose signal triggers the task to be completed.  Do not use a <see cref="Mutex"/> here.</param>
        /// <param name="timeout">The timeout (in milliseconds) after which the task will fault with a <see cref="TimeoutException"/> if the handle is not signaled by that time.</param>
        /// <returns>A Task that is completed after the handle is signaled.</returns>
        /// <remarks>
        /// There is a (brief) time delay between when the handle is signaled and when the task is marked as completed.
        /// </remarks>
        internal static Task ToTask(this WaitHandle handle, int timeout = Timeout.Infinite)
        {
            return ToTask(new WaitHandle[1] { handle }, timeout);
        }

        /// <summary>
        /// Creates a TPL Task that is marked as completed when any <see cref="WaitHandle"/> in the array is signaled.
        /// </summary>
        /// <param name="handles">The handles whose signals triggers the task to be completed.  Do not use a <see cref="Mutex"/> here.</param>
        /// <param name="timeout">The timeout (in milliseconds) after which the task will return a value of WaitTimeout.</param>
        /// <returns>A Task that is completed after any handle is signaled.</returns>
        /// <remarks>
        /// There is a (brief) time delay between when the handles are signaled and when the task is marked as completed.
        /// </remarks>
        internal static Task<int> ToTask(this WaitHandle[] handles, int timeout = Timeout.Infinite)
        {
            ErrorUtilities.VerifyThrowArgumentNull(handles, "handle");

            var tcs = new TaskCompletionSource<int>();
            int signalledHandle = WaitHandle.WaitAny(handles, 0);
            if (signalledHandle != WaitHandle.WaitTimeout)
            {
                // An optimization for if the handle is already signaled
                // to return a completed task.
                tcs.SetResult(signalledHandle);
            }
            else
            {
                var localVariableInitLock = new object();
                var culture = CultureInfo.CurrentCulture;
                var uiCulture = CultureInfo.CurrentUICulture;
                lock (localVariableInitLock)
                {
                    RegisteredWaitHandle[] callbackHandles = new RegisteredWaitHandle[handles.Length];
                    for (int i = 0; i < handles.Length; i++)
                    {
                        callbackHandles[i] = ThreadPool.RegisterWaitForSingleObject(
                            handles[i],
                            (state, timedOut) =>
                            {
                                int handleIndex = (int)state;
                                if (timedOut)
                                {
                                    tcs.TrySetResult(WaitHandle.WaitTimeout);
                                }
                                else
                                {
                                    tcs.TrySetResult(handleIndex);
                                }

                                // We take a lock here to make sure the outer method has completed setting the local variable callbackHandles contents.
                                lock (localVariableInitLock)
                                {
                                    foreach (var handle in callbackHandles)
                                    {
                                        handle.Unregister(null);
                                    }
                                }
                            },
                            state: i,
                            millisecondsTimeOutInterval: timeout,
                            executeOnlyOnce: true);
                    }
                }
            }

            return tcs.Task;
        }

        /// <summary>
        /// A class which acts as a task scheduler and ensures each scheduled task gets its 
        /// own STA thread.
        /// </summary>
        private class OneSTAThreadPerTaskScheduler : TaskScheduler
        {
            /// <summary>
            /// The current queue of tasks.
            /// </summary>
            private ConcurrentQueue<Task> _queuedTasks = new ConcurrentQueue<Task>();

            /// <summary>
            /// Returns the list of queued tasks.
            /// </summary>
            protected override System.Collections.Generic.IEnumerable<Task> GetScheduledTasks()
            {
                return _queuedTasks;
            }

            /// <summary>
            /// Queues a task to the scheduler.
            /// </summary>
            protected override void QueueTask(Task task)
            {
                _queuedTasks.Enqueue(task);

                ParameterizedThreadStart threadStart = new ParameterizedThreadStart((_) =>
                {
                    Task t;
                    if (_queuedTasks.TryDequeue(out t))
                    {
                        base.TryExecuteTask(t);
                    }
                });

                Thread thread = new Thread(threadStart);
#if FEATURE_APARTMENT_STATE
                thread.SetApartmentState(ApartmentState.STA);
#endif
                thread.Start(task);
            }

            /// <summary>
            /// Tries to execute the task immediately.  This method will always return false for the STA scheduler.
            /// </summary>
            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                // We don't get STA threads back here, so just deny the inline execution.
                return false;
            }
        }
    }
}
