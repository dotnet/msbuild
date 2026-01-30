// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
#if !CLR2COMPATIBILITY
using System.Threading.Tasks;
#endif
using Microsoft.Build.BackEnd;

#nullable disable

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Represents the execution context for a single task running in the TaskHost.
    /// Multiple contexts may exist concurrently when tasks yield or await callbacks
    /// like BuildProjectFile.
    /// </summary>
    /// <remarks>
    /// This class is used to isolate state between concurrent task executions.
    /// When Task A calls BuildProjectFile and blocks, Task B may be dispatched
    /// to the same TaskHost process. Each task needs its own:
    /// - Configuration and parameters
    /// - Pending callback requests (for request/response correlation)
    /// - Saved environment (for context switching on yield)
    /// - Completion signaling
    /// </remarks>
    internal sealed class TaskExecutionContext : IDisposable
    {
        /// <summary>
        /// Unique identifier for this task execution.
        /// Assigned by the parent process and transmitted in TaskHostConfiguration.
        /// </summary>
        public int TaskId { get; }

        /// <summary>
        /// The configuration packet that initiated this task execution.
        /// </summary>
        public TaskHostConfiguration Configuration { get; }

        /// <summary>
        /// The thread executing this task, or null if not yet started.
        /// </summary>
        public Thread ExecutingThread { get; set; }

        /// <summary>
        /// Current execution state of this task.
        /// </summary>
        public TaskExecutionState State { get; set; }

        /// <summary>
        /// Saved current directory when task yields or awaits a blocking callback.
        /// Used to restore environment when task resumes.
        /// </summary>
        public string SavedCurrentDirectory { get; set; }

        /// <summary>
        /// Saved environment variables when task yields or awaits a blocking callback.
        /// Used to restore environment when task resumes.
        /// </summary>
        public IDictionary<string, string> SavedEnvironment { get; set; }

#if !CLR2COMPATIBILITY
        /// <summary>
        /// Pending callback requests for THIS task, keyed by request ID.
        /// Each task has isolated pending requests to prevent cross-contamination
        /// when multiple tasks are blocked on callbacks simultaneously.
        /// </summary>
        public ConcurrentDictionary<int, TaskCompletionSource<INodePacket>> PendingCallbackRequests { get; }
            = new ConcurrentDictionary<int, TaskCompletionSource<INodePacket>>();
#endif

        /// <summary>
        /// Event signaled when this task completes execution.
        /// </summary>
        public ManualResetEvent CompletedEvent { get; } = new ManualResetEvent(false);

        /// <summary>
        /// The result packet to send when task completes.
        /// </summary>
        public TaskHostTaskComplete ResultPacket { get; set; }

        /// <summary>
        /// Event signaled when this specific task is cancelled.
        /// </summary>
        public ManualResetEvent CancelledEvent { get; } = new ManualResetEvent(false);

        /// <summary>
        /// Creates a new task execution context.
        /// </summary>
        /// <param name="taskId">Unique identifier for this task execution.</param>
        /// <param name="configuration">The configuration packet for this task.</param>
        public TaskExecutionContext(int taskId, TaskHostConfiguration configuration)
        {
            TaskId = taskId;
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            State = TaskExecutionState.Pending;
        }

        /// <summary>
        /// Disposes resources held by this context.
        /// </summary>
        public void Dispose()
        {
            CompletedEvent?.Dispose();
            CancelledEvent?.Dispose();
        }
    }

    /// <summary>
    /// Execution states for a task running in the TaskHost.
    /// </summary>
    internal enum TaskExecutionState
    {
        /// <summary>
        /// Task context created but execution not yet started.
        /// </summary>
        Pending,

        /// <summary>
        /// Task is actively executing on its thread.
        /// </summary>
        Executing,

        /// <summary>
        /// Task has called Yield and is waiting for Reacquire.
        /// Another task may be executing in this TaskHost.
        /// </summary>
        Yielded,

        /// <summary>
        /// Task is blocked waiting for a callback response (e.g., BuildProjectFile).
        /// Another task may be executing in this TaskHost.
        /// </summary>
        BlockedOnCallback,

        /// <summary>
        /// Task has finished execution (success or failure).
        /// </summary>
        Completed,

        /// <summary>
        /// Task was cancelled before or during execution.
        /// </summary>
        Cancelled
    }
}
