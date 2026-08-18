// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Represents the execution context for a single task running in the TaskHost.
    /// Multiple contexts may exist when a task blocks on a BuildProjectFile callback
    /// and a nested task is dispatched to the same process.
    /// </summary>
    /// <remarks>
    /// When Task A calls BuildProjectFile and blocks, Task B may be dispatched
    /// to the same TaskHost process. Each task needs its own:
    /// - Configuration and parameters
    /// - Pending callback requests (for request/response correlation)
    /// - Saved environment (for context switching on callback blocking)
    /// </remarks>
    internal sealed class TaskExecutionContext : IDisposable
    {
        /// <summary>
        /// Unique identifier for this task execution.
        /// </summary>
        public int TaskId { get; }

        /// <summary>
        /// The configuration packet that initiated this task execution.
        /// </summary>
        public TaskHostConfiguration Configuration { get; }

        /// <summary>
        /// The thread executing this task, or null if not yet started.
        /// </summary>
        public Thread? ExecutingThread { get; set; }

        /// <summary>
        /// Current execution state of this task.
        /// </summary>
        public TaskExecutionState State { get; set; }

        /// <summary>
        /// Saved current directory when task blocks on a BuildProjectFile callback.
        /// </summary>
        public string? SavedCurrentDirectory { get; set; }

        /// <summary>
        /// Saved environment variables when task blocks on a BuildProjectFile callback.
        /// </summary>
        public IDictionary<string, string>? SavedEnvironment { get; set; }

        /// <summary>
        /// Per-task warning settings from the task's configuration. Read via EffectiveWarningsAs* properties.
        /// </summary>
        public ICollection<string>? WarningsAsErrors { get; set; }

        /// <summary>Per-task WarningsNotAsErrors from the task's configuration.</summary>
        public ICollection<string>? WarningsNotAsErrors { get; set; }

        /// <summary>Per-task WarningsAsMessages from the task's configuration.</summary>
        public ICollection<string>? WarningsAsMessages { get; set; }

        /// <summary>
        /// The task wrapper for this execution, used for cancellation.
        /// Stored per-task to prevent stale references when nested tasks overwrite the shared field.
        /// </summary>
        public OutOfProcTaskAppDomainWrapper? TaskWrapper { get; set; }

        /// <summary>
        /// Saved _debugCommunications setting for this task.
        /// </summary>
        public bool SavedDebugCommunications { get; set; }

        /// <summary>
        /// Saved _updateEnvironment setting for this task.
        /// </summary>
        public bool SavedUpdateEnvironment { get; set; }

        /// <summary>
        /// Saved _updateEnvironmentAndLog setting for this task.
        /// </summary>
        public bool SavedUpdateEnvironmentAndLog { get; set; }

        /// <summary>
        /// Pending callback requests for THIS task, keyed by request ID.
        /// Each task has isolated pending requests to prevent cross-contamination
        /// when multiple tasks are blocked on callbacks simultaneously.
        /// </summary>
        public ConcurrentDictionary<int, TaskCompletionSource<INodePacket>> PendingCallbackRequests { get; } = new();

        /// <summary>
        /// Creates a new task execution context.
        /// </summary>
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
        }
    }

    /// <summary>
    /// Execution states for a task running in the TaskHost.
    /// </summary>
    internal enum TaskExecutionState
    {
        /// <summary>Task context created but not yet started.</summary>
        Pending,
        /// <summary>Task is actively executing on its thread.</summary>
        Executing,
        /// <summary>Task is blocked waiting for a callback response (e.g., BuildProjectFile).</summary>
        BlockedOnCallback,
        /// <summary>Task has finished execution.</summary>
        Completed,
    }
}
