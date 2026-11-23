// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
#if FEATURE_APPDOMAIN
using System.Runtime.Remoting;
#endif
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Components.Caching;
using Microsoft.Build.Eventing;
#if FEATURE_REPORTFILEACCESSES
using Microsoft.Build.Experimental.FileAccess;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Represents the execution context and per-task <see cref="IBuildEngine10"/> proxy for a single
    /// task running in the out-of-proc TaskHost.
    /// </summary>
    /// <remarks>
    /// Passing a dedicated proxy to each task execution mirrors the in-process <c>TaskHost</c>:
    /// <list type="bullet">
    /// <item><description>Background threads spawned by a task hold that task's proxy and stop logging as soon as the task completes, even if the TaskHost process immediately runs another task or still holds an outer blocked task.</description></item>
    /// <item><description>Long-lived worker threads shared across task invocations use the proxy of whichever task invoked them rather than stale ambient <see cref="AsyncLocal{T}"/> state.</description></item>
    /// <item><description>Immutable location metadata remains readable after completion so <c>TaskLoggingHelper</c> never faults on cleared configuration before reaching <c>Log*Event</c>.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class TaskExecutionContext :
#if FEATURE_APPDOMAIN
        MarshalByRefObject,
#endif
        IBuildEngine10,
        IDisposable
    {
        private readonly bool _continueOnError;
        private readonly string _projectFileOfTask;
        private OutOfProcTaskHostNode? _owner;
        private TaskHostConfiguration? _configuration;
        private RegisteredTaskObjectCacheBase? _registeredTaskObjectCache;
        private IDictionary<string, string>? _globalProperties;
        private ICollection<string>? _warningsAsErrors;
        private ICollection<string>? _warningsNotAsErrors;
        private ICollection<string>? _warningsAsMessages;
#if FEATURE_REPORTFILEACCESSES
        private List<FileAccessData>? _fileAccessData;
#endif
        private int _state;

        /// <summary>
        /// Synchronizes task deactivation with late logging, file-access reporting, and cancellation.
        /// Never held across blocking IPC callbacks.
        /// </summary>
        internal LockType CallbackMonitor { get; } = new();

        /// <summary>
        /// Unique identifier for this task execution.
        /// </summary>
        public int TaskId { get; }

        /// <summary>
        /// Owning worker node ID recorded in outgoing log packets.
        /// </summary>
        public int NodeId { get; }

        /// <summary>
        /// Name of the executing task.
        /// </summary>
        public string TaskName { get; }

        /// <summary>
        /// Whether task input logging is enabled for this task's build.
        /// </summary>
        public bool IsTaskInputLoggingEnabled { get; }

        /// <summary>
        /// The configuration packet that initiated this task execution, or <see langword="null"/> after teardown.
        /// </summary>
        public TaskHostConfiguration? Configuration => Volatile.Read(ref _configuration);

        /// <summary>
        /// The thread executing this task, or null if not yet started.
        /// </summary>
        public Thread? ExecutingThread { get; set; }

        /// <summary>
        /// Current execution state of this task.
        /// </summary>
        public TaskExecutionState State
        {
            get => (TaskExecutionState)Volatile.Read(ref _state);
            set => Volatile.Write(ref _state, (int)value);
        }

        /// <summary>
        /// Whether this task context is still active and permitted to interact with the host.
        /// </summary>
        internal bool IsActive => State != TaskExecutionState.Completed;

        /// <summary>Whether this task may fail without logging an error.</summary>
        public bool AllowFailureWithoutError { get; set; }

        /// <summary>
        /// Saved current directory when task blocks on a BuildProjectFile callback.
        /// </summary>
        public string? SavedCurrentDirectory { get; set; }

        /// <summary>
        /// Saved environment variables when task blocks on a BuildProjectFile callback.
        /// </summary>
        public IDictionary<string, string>? SavedEnvironment { get; set; }

        /// <summary>
        /// Per-task warning settings from the task's configuration.
        /// </summary>
        public ICollection<string>? WarningsAsErrors
        {
            get => Volatile.Read(ref _warningsAsErrors);
            set => Volatile.Write(ref _warningsAsErrors, value);
        }

        /// <summary>Per-task WarningsNotAsErrors from the task's configuration.</summary>
        public ICollection<string>? WarningsNotAsErrors
        {
            get => Volatile.Read(ref _warningsNotAsErrors);
            set => Volatile.Write(ref _warningsNotAsErrors, value);
        }

        /// <summary>Per-task WarningsAsMessages from the task's configuration.</summary>
        public ICollection<string>? WarningsAsMessages
        {
            get => Volatile.Read(ref _warningsAsMessages);
            set => Volatile.Write(ref _warningsAsMessages, value);
        }

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

        /// <inheritdoc />
        public EngineServices EngineServices { get; }

        /// <summary>
        /// Creates a new task execution context.
        /// </summary>
        public TaskExecutionContext(int taskId, TaskHostConfiguration configuration, OutOfProcTaskHostNode? owner = null)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            TaskId = taskId;
            _configuration = configuration;
            _owner = owner;
            _registeredTaskObjectCache = owner?.RegisteredTaskObjectCache;

            NodeId = configuration.NodeId;
            TaskName = configuration.TaskName ?? string.Empty;
            _projectFileOfTask = configuration.ProjectFileOfTask ?? string.Empty;
            LineNumberOfTaskNode = configuration.LineNumberOfTask;
            ColumnNumberOfTaskNode = configuration.ColumnNumberOfTask;
            _continueOnError = configuration.ContinueOnError;
            IsTaskInputLoggingEnabled = configuration.IsTaskInputLoggingEnabled;
            _globalProperties = configuration.GlobalProperties;
            _warningsAsErrors = configuration.WarningsAsErrors;
            _warningsNotAsErrors = configuration.WarningsNotAsErrors;
            _warningsAsMessages = configuration.WarningsAsMessages;

            State = TaskExecutionState.Pending;
            EngineServices = new EngineServicesImpl(this);
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Gives the task proxy an infinite remoting lease for the duration of task execution;
        /// <see cref="Dispose"/> explicitly disconnects it once the task's AppDomain is unloaded.
        /// </summary>
        public override object? InitializeLifetimeService() => null;
#endif

        /// <summary>
        /// Atomically marks this task execution inactive so no further build events, callbacks, or file accesses
        /// can be sent before <see cref="TaskHostTaskComplete"/> is enqueued.
        /// </summary>
        internal void MarkAsInactive(bool reclaimNodeCounts = false)
        {
            List<TaskCompletionSource<INodePacket>>? abandonedCallbacks = null;

            lock (CallbackMonitor)
            {
                TaskExecutionState previousState = State;
                State = TaskExecutionState.Completed;

                if (reclaimNodeCounts && _owner is not null)
                {
                    _owner.ReclaimTaskCountsOnCompletion(previousState);
                }

                foreach (KeyValuePair<int, TaskCompletionSource<INodePacket>> pending in PendingCallbackRequests)
                {
                    if (PendingCallbackRequests.TryRemove(pending.Key, out TaskCompletionSource<INodePacket>? tcs))
                    {
                        _owner?.RecordAbandonedCallbackRequest(pending.Key);
                        abandonedCallbacks ??= [];
                        abandonedCallbacks.Add(tcs);
                    }
                }
            }

            if (abandonedCallbacks is not null)
            {
                InvalidOperationException ex = new("Task completed before BuildEngine callback finished.");
                foreach (TaskCompletionSource<INodePacket> tcs in abandonedCallbacks)
                {
                    tcs.TrySetException(ex);
                }
            }
        }

        /// <summary>
        /// Releases references to heavy per-task objects so a task that leaks <see cref="ITask.BuildEngine"/>
        /// into a static field cannot root task parameters or build environments across the TaskHost lifetime.
        /// </summary>
        internal void ReleaseTaskReferences()
        {
            lock (CallbackMonitor)
            {
                State = TaskExecutionState.Completed;
                _owner = null;
                _configuration = null;
                _globalProperties = null;
                _warningsAsErrors = null;
                _warningsNotAsErrors = null;
                _warningsAsMessages = null;
                TaskWrapper = null;
                ExecutingThread = null;
                SavedCurrentDirectory = null;
                SavedEnvironment = null;
#if FEATURE_REPORTFILEACCESSES
                _fileAccessData = null;
#endif
            }
        }

        internal void VerifyActiveProxy()
        {
            Assumed.True(IsActive, "Attempted to use an inactive task host.");
        }

        private OutOfProcTaskHostNode GetActiveOwner()
        {
            VerifyActiveProxy();
            OutOfProcTaskHostNode? owner = Volatile.Read(ref _owner);
            Assumed.NotNull(owner, "Attempted to use an inactive task host.");
            return owner;
        }

        #region IBuildEngine Properties

        /// <inheritdoc />
        public bool ContinueOnError
        {
            get
            {
                VerifyActiveProxy();
                return _continueOnError;
            }
        }

        /// <inheritdoc />
        public int LineNumberOfTaskNode { get; }

        /// <inheritdoc />
        public int ColumnNumberOfTaskNode { get; }

        /// <inheritdoc />
        public string ProjectFileOfTaskNode => _projectFileOfTask;

        #endregion

        #region IBuildEngine Methods

        /// <inheritdoc />
        public void LogErrorEvent(BuildErrorEventArgs e) => LogBuildEvent(e);

        /// <inheritdoc />
        public void LogWarningEvent(BuildWarningEventArgs e) => LogBuildEvent(e);

        /// <inheritdoc />
        public void LogMessageEvent(BuildMessageEventArgs e) => LogBuildEvent(e);

        /// <inheritdoc />
        public void LogCustomEvent(CustomBuildEventArgs e) => LogBuildEvent(e);

        internal void LogBuildEvent(BuildEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            lock (CallbackMonitor)
            {
                if (!IsActive)
                {
                    OutOfProcTaskHostNode.DropBuildEventFromCompletedTask(TaskName, e.Message);
                    return;
                }

                _owner?.SendBuildEventCore(
                    NodeId,
                    TaskName,
                    _projectFileOfTask,
                    LineNumberOfTaskNode,
                    ColumnNumberOfTaskNode,
                    e);
            }
        }

        /// <inheritdoc />
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
            => BuildProjectFile(projectFileName, targetNames, globalProperties, targetOutputs, null);

        #endregion

        #region IBuildEngine2 Members

        /// <inheritdoc />
        public bool IsRunningMultipleNodes
        {
            get
            {
                OutOfProcTaskHostNode owner = GetActiveOwner();
                if (!owner.CallbacksSupported)
                {
                    LogErrorFromResource(owner, "BuildEngineCallbacksInTaskHostUnsupported");
                    return false;
                }

                var request = new TaskHostIsRunningMultipleNodesRequest();
                var response = owner.SendCallbackRequestAndWaitForResponse<TaskHostIsRunningMultipleNodesResponse>(request, this);
                return response.IsRunningMultipleNodes;
            }
        }

        /// <inheritdoc />
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string? toolsVersion)
        {
            OutOfProcTaskHostNode owner = GetActiveOwner();
            if (!owner.CallbacksSupported)
            {
                LogErrorFromResource(owner, "BuildEngineCallbacksInTaskHostUnsupported");
                return false;
            }

            return BuildProjectFilesInParallel(
                [projectFileName],
                targetNames,
                [globalProperties],
                [targetOutputs],
                [toolsVersion!],
                useResultsCache: true,
                unloadProjectsOnCompletion: false);
        }

        /// <inheritdoc />
        public bool BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersion,
            bool useResultsCache,
            bool unloadProjectsOnCompletion)
        {
            OutOfProcTaskHostNode owner = GetActiveOwner();
            if (!owner.CallbacksSupported)
            {
                LogErrorFromResource(owner, "BuildEngineCallbacksInTaskHostUnsupported");
                return false;
            }

            if (projectFileNames is null)
            {
                return false;
            }

            Assumed.True(
                targetOutputsPerProject is null || projectFileNames.Length == targetOutputsPerProject.Length,
                $"projectFileNames has {projectFileNames.Length} entries but targetOutputsPerProject has {targetOutputsPerProject?.Length ?? 0} -- lengths must match.");

            bool includeTargetOutputs = targetOutputsPerProject is not null;

            BuildEngineResult result = BuildProjectFilesInParallel(
                projectFileNames,
                targetNames,
                globalProperties,
                new IList<string>[projectFileNames.Length],
                toolsVersion,
                includeTargetOutputs);

            if (includeTargetOutputs && result.TargetOutputsPerProject is not null)
            {
                for (int i = 0; i < targetOutputsPerProject!.Length && i < result.TargetOutputsPerProject.Count; i++)
                {
                    if (targetOutputsPerProject[i] is not null)
                    {
                        foreach (KeyValuePair<string, ITaskItem[]> output in result.TargetOutputsPerProject[i])
                        {
                            targetOutputsPerProject[i].Add(output.Key, output.Value);
                        }
                    }
                }
            }

            return result.Result;
        }

        #endregion

        #region IBuildEngine3 Members

        /// <inheritdoc />
        public BuildEngineResult BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] removeGlobalProperties,
            string[] toolsVersion,
            bool returnTargetOutputs)
        {
            OutOfProcTaskHostNode owner = GetActiveOwner();
            if (!owner.CallbacksSupported)
            {
                LogErrorFromResource(owner, "BuildEngineCallbacksInTaskHostUnsupported");
                return new BuildEngineResult(false, null!);
            }

            string? projectFilesJoined = null;

            if (MSBuildEventSource.Log.IsEnabled())
            {
                projectFilesJoined = string.Join(";", projectFileNames ?? []);
                string targetNamesJoined = string.Join(";", targetNames ?? []);
                MSBuildEventSource.Log.TaskHostBuildProjectFileStart(projectFilesJoined, targetNamesJoined);
            }

            var request = new TaskHostBuildRequest(
                projectFileNames,
                targetNames,
                TaskHostBuildRequest.ConvertGlobalProperties(globalProperties),
                TaskHostBuildRequest.ConvertRemoveGlobalProperties(removeGlobalProperties),
                toolsVersion,
                returnTargetOutputs);

            owner.BlockForCallback(this);
            bool success = false;
            try
            {
                var response = owner.SendCallbackRequestAndWaitForResponse<TaskHostBuildResponse>(request, this);
                BuildEngineResult result = response.ToBuildEngineResult();
                success = result.Result;
                return result;
            }
            finally
            {
                if (MSBuildEventSource.Log.IsEnabled())
                {
                    MSBuildEventSource.Log.TaskHostBuildProjectFileStop(projectFilesJoined!, success);
                }

                owner.ResumeAfterCallback(this);
            }
        }

        /// <inheritdoc />
        public void Yield()
        {
        }

        /// <inheritdoc />
        public void Reacquire()
        {
        }

        #endregion

        #region IBuildEngine4 Members

        /// <inheritdoc />
        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            if (lifetime == RegisteredTaskObjectLifetime.Build)
            {
                VerifyActiveProxy();
            }

            RegisteredTaskObjectCacheBase? cache = Volatile.Read(ref _registeredTaskObjectCache);
            Assumed.NotNull(cache, "Attempted to use an inactive task host.");
            cache.RegisterTaskObject(key, obj, lifetime, allowEarlyCollection);
        }

        /// <inheritdoc />
        public object? GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            => Volatile.Read(ref _registeredTaskObjectCache)?.GetRegisteredTaskObject(key, lifetime);

        /// <inheritdoc />
        public object? UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            => Volatile.Read(ref _registeredTaskObjectCache)?.UnregisterTaskObject(key, lifetime);

        #endregion

        #region IBuildEngine5 Members

        /// <inheritdoc />
        public void LogTelemetry(string eventName, IDictionary<string, string>? properties)
        {
            lock (CallbackMonitor)
            {
                if (!IsActive)
                {
                    OutOfProcTaskHostNode.DropBuildEventFromCompletedTask(TaskName, eventName);
                    return;
                }

                IDictionary<string, string?> copiedProperties = new Dictionary<string, string?>(properties?.Count ?? 0);
                if (properties is not null)
                {
                    foreach (KeyValuePair<string, string> pair in properties)
                    {
                        copiedProperties[pair.Key] = pair.Value;
                    }
                }

                TelemetryEventArgs telemetry = new()
                {
                    EventName = eventName,
                    Properties = copiedProperties,
                };

                _owner?.SendBuildEventCore(
                    NodeId,
                    TaskName,
                    _projectFileOfTask,
                    LineNumberOfTaskNode,
                    ColumnNumberOfTaskNode,
                    telemetry);
            }
        }

        #endregion

        #region IBuildEngine6 Members

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> GetGlobalProperties()
        {
            VerifyActiveProxy();
            IDictionary<string, string>? globalProperties = Volatile.Read(ref _globalProperties);
            return globalProperties is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(globalProperties);
        }

        #endregion

        #region IBuildEngine8 Members

        /// <inheritdoc />
        public bool ShouldTreatWarningAsError(string warningCode)
            => OutOfProcTaskHostNode.ShouldTreatWarningAsErrorCore(
                warningCode,
                WarningsAsErrors,
                WarningsNotAsErrors,
                WarningsAsMessages);

        #endregion

        #region IBuildEngine9 Members

        /// <inheritdoc />
        public int RequestCores(int requestedCores)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedCores);

            OutOfProcTaskHostNode owner = GetActiveOwner();
            if (!owner.CallbacksSupported)
            {
                throw new NotImplementedException();
            }

            var request = new TaskHostCoresRequest(requestedCores, isRelease: false);
            var response = owner.SendCallbackRequestAndWaitForResponse<TaskHostCoresResponse>(request, this);
            return response.GrantedCores;
        }

        /// <inheritdoc />
        public void ReleaseCores(int coresToRelease)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(coresToRelease);

            OutOfProcTaskHostNode owner = GetActiveOwner();
            if (!owner.CallbacksSupported)
            {
                throw new NotImplementedException();
            }

            var request = new TaskHostCoresRequest(coresToRelease, isRelease: true);
            owner.SendCallbackRequestAndWaitForResponse<TaskHostCoresResponse>(request, this);
        }

        #endregion

        private void LogErrorFromResource(OutOfProcTaskHostNode owner, string messageResource)
        {
            lock (CallbackMonitor)
            {
                if (!IsActive)
                {
                    return;
                }

                BuildErrorEventArgs error = new(
                    subcategory: null,
                    code: null,
                    file: _projectFileOfTask,
                    lineNumber: LineNumberOfTaskNode,
                    columnNumber: ColumnNumberOfTaskNode,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message: AssemblyResources.GetString(messageResource),
                    helpKeyword: null,
                    senderName: TaskName);

                owner.SendBuildEventCore(
                    NodeId,
                    TaskName,
                    _projectFileOfTask,
                    LineNumberOfTaskNode,
                    ColumnNumberOfTaskNode,
                    error);
            }
        }

#if FEATURE_REPORTFILEACCESSES
        internal void ReportFileAccess(FileAccessData fileAccessData)
        {
            lock (CallbackMonitor)
            {
                if (!IsActive)
                {
                    return;
                }

                _fileAccessData ??= [];
                _fileAccessData.Add(fileAccessData);
            }
        }

        internal List<FileAccessData>? TakeFileAccessData()
        {
            lock (CallbackMonitor)
            {
                List<FileAccessData>? data = _fileAccessData;
                _fileAccessData = null;
                return data;
            }
        }
#endif

        [Serializable]
        private sealed class EngineServicesImpl : EngineServices
        {
            private readonly TaskExecutionContext _context;

            internal EngineServicesImpl(TaskExecutionContext context)
            {
                _context = context;
            }

            public override bool LogsMessagesOfImportance(MessageImportance importance) => true;

            public override bool IsTaskInputLoggingEnabled => _context.IsTaskInputLoggingEnabled;

#if FEATURE_REPORTFILEACCESSES
            public void ReportFileAccess(FileAccessData fileAccessData) => _context.ReportFileAccess(fileAccessData);
#endif
        }

        /// <summary>
        /// Disposes resources held by this context and disconnects any AppDomain remoting identity.
        /// </summary>
        public void Dispose()
        {
            ReleaseTaskReferences();
#if FEATURE_APPDOMAIN
            RemotingServices.Disconnect(this);
#endif
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
