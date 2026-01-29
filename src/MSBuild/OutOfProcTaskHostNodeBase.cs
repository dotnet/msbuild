// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
#if !CLR2COMPATIBILITY
using Microsoft.Build.Experimental.FileAccess;
#endif
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
#if FEATURE_APPDOMAIN
using System.Runtime.Remoting;
#endif

#nullable disable

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Base class for out-of-proc task host nodes. Contains shared functionality for both
    /// regular TaskHostFactory taskhosts and long-lived Sidecar taskhosts.
    /// </summary>
    internal abstract class OutOfProcTaskHostNodeBase :
#if FEATURE_APPDOMAIN
        MarshalByRefObject,
#endif
        INodePacketFactory, INodePacketHandler,
#if CLR2COMPATIBILITY
        IBuildEngine3
#else
        IBuildEngine10
#endif
    {
        /// <summary>
        /// Keeps a record of all environment variables that, on startup of the task host, have a different
        /// value from those that are passed to the task host in the configuration packet for the first task.
        /// </summary>
        private static IDictionary<string, KeyValuePair<string, string>> s_mismatchedEnvironmentValues;

        /// <summary>
        /// The endpoint used to talk to the host.
        /// </summary>
        protected NodeEndpointOutOfProcTaskHost _nodeEndpoint;

        /// <summary>
        /// The packet factory.
        /// </summary>
        private NodePacketFactory _packetFactory;

        /// <summary>
        /// The event which is set when we receive packets.
        /// </summary>
        private AutoResetEvent _packetReceivedEvent;

        /// <summary>
        /// The queue of packets we have received but which have not yet been processed.
        /// </summary>
        private Queue<INodePacket> _receivedPackets;

        /// <summary>
        /// The current configuration for this task host.
        /// </summary>
        protected TaskHostConfiguration _currentConfiguration;

        /// <summary>
        /// The saved environment for the process.
        /// </summary>
        private IDictionary<string, string> _savedEnvironment;

        /// <summary>
        /// The event which is set when we should shut down.
        /// </summary>
        private ManualResetEvent _shutdownEvent;

        /// <summary>
        /// The reason we are shutting down.
        /// </summary>
        private NodeEngineShutdownReason _shutdownReason;

        /// <summary>
        /// We set this flag to track a currently executing task
        /// </summary>
        protected bool _isTaskExecuting;

        /// <summary>
        /// The event which is set when a task has completed.
        /// </summary>
        private AutoResetEvent _taskCompleteEvent;

        /// <summary>
        /// Packet containing all the information relating to the
        /// completed state of the task.
        /// </summary>
        private TaskHostTaskComplete _taskCompletePacket;

        /// <summary>
        /// Object used to synchronize access to taskCompletePacket
        /// </summary>
        private LockType _taskCompleteLock = new();

        /// <summary>
        /// The event which is set when a task is cancelled
        /// </summary>
        protected ManualResetEvent _taskCancelledEvent;

        /// <summary>
        /// The thread currently executing user task in the TaskRunner
        /// </summary>
        private Thread _taskRunnerThread;

        /// <summary>
        /// This is the wrapper for the user task to be executed.
        /// We are providing a wrapper to create a possibility of executing the task in a separate AppDomain
        /// </summary>
        private OutOfProcTaskAppDomainWrapper _taskWrapper;

        /// <summary>
        /// Flag indicating if we should debug communications or not.
        /// </summary>
        private bool _debugCommunications;

        /// <summary>
        /// Flag indicating whether we should modify the environment based on any differences we find between that of the
        /// task host at startup and the environment passed to us in our initial task configuration packet.
        /// </summary>
        private bool _updateEnvironment;

        /// <summary>
        /// An interim step between MSBuildTaskHostDoNotUpdateEnvironment=1 and the default update behavior.
        /// </summary>
        private bool _updateEnvironmentAndLog;

        /// <summary>
        /// Setting this to true means we're running a long-lived sidecar node.
        /// </summary>
        protected bool _nodeReuse;

#if !CLR2COMPATIBILITY
        /// <summary>
        /// The task object cache.
        /// </summary>
        private RegisteredTaskObjectCacheBase _registeredTaskObjectCache;
#endif

#if FEATURE_REPORTFILEACCESSES
        /// <summary>
        /// The file accesses reported by the most recently completed task.
        /// </summary>
        private List<FileAccessData> _fileAccessData = new List<FileAccessData>();
#endif

        /// <summary>
        /// Constructor.
        /// </summary>
        protected OutOfProcTaskHostNodeBase()
        {
            _debugCommunications = Traits.Instance.DebugNodeCommunication;

            _receivedPackets = new Queue<INodePacket>();

            // These WaitHandles are disposed in HandleShutDown()
            _packetReceivedEvent = new AutoResetEvent(false);
            _shutdownEvent = new ManualResetEvent(false);
            _taskCompleteEvent = new AutoResetEvent(false);
            _taskCancelledEvent = new ManualResetEvent(false);

            _packetFactory = new NodePacketFactory();

            INodePacketFactory thisINodePacketFactory = (INodePacketFactory)this;

            thisINodePacketFactory.RegisterPacketHandler(NodePacketType.TaskHostConfiguration, TaskHostConfiguration.FactoryForDeserialization, this);
            thisINodePacketFactory.RegisterPacketHandler(NodePacketType.TaskHostTaskCancelled, TaskHostTaskCancelled.FactoryForDeserialization, this);
            thisINodePacketFactory.RegisterPacketHandler(NodePacketType.NodeBuildComplete, NodeBuildComplete.FactoryForDeserialization, this);

#if !CLR2COMPATIBILITY
            EngineServices = new EngineServicesImpl(this);
#endif
        }

        #region IBuildEngine Implementation (Properties)

        /// <summary>
        /// Returns the value of ContinueOnError for the currently executing task.
        /// </summary>
        public bool ContinueOnError
        {
            get
            {
                ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration during a BuildEngine callback!");
                return _currentConfiguration.ContinueOnError;
            }
        }

        /// <summary>
        /// Returns the line number of the location in the project file of the currently executing task.
        /// </summary>
        public int LineNumberOfTaskNode
        {
            get
            {
                ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration during a BuildEngine callback!");
                return _currentConfiguration.LineNumberOfTask;
            }
        }

        /// <summary>
        /// Returns the column number of the location in the project file of the currently executing task.
        /// </summary>
        public int ColumnNumberOfTaskNode
        {
            get
            {
                ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration during a BuildEngine callback!");
                return _currentConfiguration.ColumnNumberOfTask;
            }
        }

        /// <summary>
        /// Returns the project file of the currently executing task.
        /// </summary>
        public string ProjectFileOfTaskNode
        {
            get
            {
                ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration during a BuildEngine callback!");
                return _currentConfiguration.ProjectFileOfTask;
            }
        }

        #endregion // IBuildEngine Implementation (Properties)

        #region IBuildEngine2 Implementation (Properties)

        /// <summary>
        /// Gets whether we're running multiple nodes. Derived classes implement this differently.
        /// </summary>
        public abstract bool IsRunningMultipleNodes { get; }

        #endregion // IBuildEngine2 Implementation (Properties)

        #region IBuildEngine7 Implementation
        /// <summary>
        /// Enables or disables emitting a default error when a task fails without logging errors
        /// </summary>
        public bool AllowFailureWithoutError { get; set; } = false;
        #endregion

        #region IBuildEngine8 Implementation

        /// <summary>
        /// Contains all warnings that should be logged as errors.
        /// </summary>
        private ICollection<string> WarningsAsErrors { get; set; }

        private ICollection<string> WarningsNotAsErrors { get; set; }

        private ICollection<string> WarningsAsMessages { get; set; }

        public bool ShouldTreatWarningAsError(string warningCode)
        {
            if (WarningsAsErrors == null || WarningsAsMessages?.Contains(warningCode) == true)
            {
                return false;
            }

            return (WarningsAsErrors.Count == 0 && WarningAsErrorNotOverriden(warningCode)) || WarningsAsMessages.Contains(warningCode);
        }

        private bool WarningAsErrorNotOverriden(string warningCode)
        {
            return WarningsNotAsErrors?.Contains(warningCode) != true;
        }
        #endregion

        #region IBuildEngine Implementation (Methods)

        /// <summary>
        /// Sends the provided error back to the parent node to be logged.
        /// </summary>
        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            SendBuildEvent(e);
        }

        /// <summary>
        /// Sends the provided warning back to the parent node to be logged.
        /// </summary>
        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            SendBuildEvent(e);
        }

        /// <summary>
        /// Sends the provided message back to the parent node to be logged.
        /// </summary>
        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            SendBuildEvent(e);
        }

        /// <summary>
        /// Sends the provided custom event back to the parent node to be logged.
        /// </summary>
        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            SendBuildEvent(e);
        }

        /// <summary>
        /// Implementation of IBuildEngine.BuildProjectFile. Derived classes implement this differently.
        /// </summary>
        public abstract bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs);

        #endregion // IBuildEngine Implementation (Methods)

        #region IBuildEngine2 Implementation (Methods)

        /// <summary>
        /// Implementation of IBuildEngine2.BuildProjectFile. Derived classes implement this differently.
        /// </summary>
        public abstract bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion);

        /// <summary>
        /// Implementation of IBuildEngine2.BuildProjectFilesInParallel. Derived classes implement this differently.
        /// </summary>
        public abstract bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion);

        #endregion // IBuildEngine2 Implementation (Methods)

        #region IBuildEngine3 Implementation

        /// <summary>
        /// Implementation of IBuildEngine3.BuildProjectFilesInParallel. Derived classes implement this differently.
        /// </summary>
        public abstract BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs);

        /// <summary>
        /// Implementation of IBuildEngine3.Yield. Derived classes implement this differently.
        /// </summary>
        public abstract void Yield();

        /// <summary>
        /// Implementation of IBuildEngine3.Reacquire. Derived classes implement this differently.
        /// </summary>
        public abstract void Reacquire();

        #endregion // IBuildEngine3 Implementation

#if !CLR2COMPATIBILITY
        #region IBuildEngine4 Implementation

        /// <summary>
        /// Registers an object with the system that will be disposed of at some specified time in the future.
        /// </summary>
        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            _registeredTaskObjectCache.RegisterTaskObject(key, obj, lifetime, allowEarlyCollection);
        }

        /// <summary>
        /// Retrieves a previously registered task object stored with the specified key.
        /// </summary>
        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            return _registeredTaskObjectCache.GetRegisteredTaskObject(key, lifetime);
        }

        /// <summary>
        /// Unregisters a previously-registered task object.
        /// </summary>
        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            return _registeredTaskObjectCache.UnregisterTaskObject(key, lifetime);
        }

        #endregion

        #region IBuildEngine5 Implementation

        /// <summary>
        /// Logs a telemetry event.
        /// </summary>
        public void LogTelemetry(string eventName, IDictionary<string, string> properties)
        {
            SendBuildEvent(new TelemetryEventArgs
            {
                EventName = eventName,
                Properties = properties == null ? new Dictionary<string, string>() : new Dictionary<string, string>(properties),
            });
        }

        #endregion

        #region IBuildEngine6 Implementation

        /// <summary>
        /// Gets the global properties for the current project.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetGlobalProperties()
        {
            return new Dictionary<string, string>(_currentConfiguration.GlobalProperties);
        }

        #endregion

        #region IBuildEngine9 Implementation

        /// <summary>
        /// Implementation of IBuildEngine9.RequestCores. Derived classes implement this differently.
        /// </summary>
        public abstract int RequestCores(int requestedCores);

        /// <summary>
        /// Implementation of IBuildEngine9.ReleaseCores. Derived classes implement this differently.
        /// </summary>
        public abstract void ReleaseCores(int coresToRelease);

        #endregion

        #region IBuildEngine10 Members

        [Serializable]
        private sealed class EngineServicesImpl : EngineServices
        {
            private readonly OutOfProcTaskHostNodeBase _taskHost;

            internal EngineServicesImpl(OutOfProcTaskHostNodeBase taskHost)
            {
                _taskHost = taskHost;
            }

            /// <summary>
            /// No logging verbosity optimization in OOP nodes.
            /// </summary>
            public override bool LogsMessagesOfImportance(MessageImportance importance) => true;

            /// <inheritdoc />
            public override bool IsTaskInputLoggingEnabled
            {
                get
                {
                    ErrorUtilities.VerifyThrow(_taskHost._currentConfiguration != null, "We should never have a null configuration during a BuildEngine callback!");
                    return _taskHost._currentConfiguration.IsTaskInputLoggingEnabled;
                }
            }

#if FEATURE_REPORTFILEACCESSES
            /// <summary>
            /// Reports a file access from a task.
            /// </summary>
            public void ReportFileAccess(FileAccessData fileAccessData)
            {
                _taskHost._fileAccessData.Add(fileAccessData);
            }
#endif
        }

        public EngineServices EngineServices { get; }

        #endregion

#endif

        #region INodePacketFactory Members

        /// <summary>
        /// Registers the specified handler for a particular packet type.
        /// </summary>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Takes a serializer, deserializes the packet and routes it to the appropriate handler.
        /// </summary>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            _packetFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Takes a serializer and deserializes the packet.
        /// </summary>
        public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator)
        {
            return _packetFactory.DeserializePacket(packetType, translator);
        }

        /// <summary>
        /// Routes the specified packet
        /// </summary>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            _packetFactory.RoutePacket(nodeId, packet);
        }

        #endregion // INodePacketFactory Members

        #region INodePacketHandler Members

        /// <summary>
        /// This method is invoked by the NodePacketRouter when a packet is received and is intended for this recipient.
        /// </summary>
        public void PacketReceived(int node, INodePacket packet)
        {
            lock (_receivedPackets)
            {
                _receivedPackets.Enqueue(packet);
                _packetReceivedEvent.Set();
            }
        }

        #endregion // INodePacketHandler Members

        #region INode Members

        /// <summary>
        /// Starts up the node and processes messages until the node is requested to shut down.
        /// </summary>
        public NodeEngineShutdownReason Run(out Exception shutdownException, bool nodeReuse = false, byte parentPacketVersion = 1)
        {
#if !CLR2COMPATIBILITY
            _registeredTaskObjectCache = new RegisteredTaskObjectCacheBase();
#endif
            shutdownException = null;

            // Snapshot the current environment
            _savedEnvironment = CommunicationsUtilities.GetEnvironmentVariables();

            _nodeReuse = nodeReuse;
            _nodeEndpoint = new NodeEndpointOutOfProcTaskHost(nodeReuse, parentPacketVersion);
            _nodeEndpoint.OnLinkStatusChanged += new LinkStatusChangedDelegate(OnLinkStatusChanged);
            _nodeEndpoint.Listen(this);

            WaitHandle[] waitHandles = [_shutdownEvent, _packetReceivedEvent, _taskCompleteEvent, _taskCancelledEvent];

            while (true)
            {
                int index = WaitHandle.WaitAny(waitHandles);
                switch (index)
                {
                    case 0: // shutdownEvent
                        NodeEngineShutdownReason shutdownReason = HandleShutdown();
                        return shutdownReason;

                    case 1: // packetReceivedEvent
                        INodePacket packet = null;

                        int packetCount = _receivedPackets.Count;

                        while (packetCount > 0)
                        {
                            lock (_receivedPackets)
                            {
                                if (_receivedPackets.Count > 0)
                                {
                                    packet = _receivedPackets.Dequeue();
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (packet != null)
                            {
                                HandlePacket(packet);
                            }
                        }

                        break;
                    case 2: // taskCompleteEvent
                        CompleteTask();
                        break;
                    case 3: // taskCancelledEvent
                        CancelTask();
                        break;
                }
            }

            // UNREACHABLE
        }
        #endregion

        /// <summary>
        /// Dispatches the packet to the correct handler. Derived classes can override to handle additional packet types.
        /// </summary>
        protected virtual void HandlePacket(INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.TaskHostConfiguration:
                    HandleTaskHostConfiguration(packet as TaskHostConfiguration);
                    break;
                case NodePacketType.TaskHostTaskCancelled:
                    _taskCancelledEvent.Set();
                    break;
                case NodePacketType.NodeBuildComplete:
                    HandleNodeBuildComplete(packet as NodeBuildComplete);
                    break;
            }
        }

        /// <summary>
        /// Configure the task host according to the information received in the configuration packet
        /// </summary>
        private void HandleTaskHostConfiguration(TaskHostConfiguration taskHostConfiguration)
        {
            ErrorUtilities.VerifyThrow(!_isTaskExecuting, "Why are we getting a TaskHostConfiguration packet while we're still executing a task?");
            _currentConfiguration = taskHostConfiguration;

            // Kick off the task running thread.
            _taskRunnerThread = new Thread(new ParameterizedThreadStart(RunTask));
            _taskRunnerThread.Name = "Task runner for task " + taskHostConfiguration.TaskName;
            _taskRunnerThread.Start(taskHostConfiguration);
        }

        /// <summary>
        /// The task has been completed
        /// </summary>
        private void CompleteTask()
        {
            ErrorUtilities.VerifyThrow(!_isTaskExecuting, "The task should be done executing before CompleteTask.");
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                TaskHostTaskComplete taskCompletePacketToSend;

                lock (_taskCompleteLock)
                {
                    ErrorUtilities.VerifyThrowInternalNull(_taskCompletePacket, "taskCompletePacket");
                    taskCompletePacketToSend = _taskCompletePacket;
                    _taskCompletePacket = null;
                }

                _nodeEndpoint.SendData(taskCompletePacketToSend);
            }

            _currentConfiguration = null;

            // If the task has been canceled, the event will still be set.
            if (_taskCancelledEvent.WaitOne(0))
            {
                _shutdownReason = NodeEngineShutdownReason.BuildComplete;
                _shutdownEvent.Set();
            }
        }

        /// <summary>
        /// This task has been cancelled. Attempt to cancel the task
        /// </summary>
        private void CancelTask()
        {
            var wrapper = _taskWrapper;
            if (wrapper?.CancelTask() == false)
            {
                if (Environment.GetEnvironmentVariable("MSBUILDTASKHOSTABORTTASKONCANCEL") == "1")
                {
                    if (_isTaskExecuting)
                    {
#if FEATURE_THREAD_ABORT
                        _taskRunnerThread.Abort();
#endif
                    }
                }
            }
        }

        /// <summary>
        /// Handles the NodeBuildComplete packet.
        /// </summary>
        private void HandleNodeBuildComplete(NodeBuildComplete buildComplete)
        {
            ErrorUtilities.VerifyThrow(!_isTaskExecuting, "We should never have a task in the process of executing when we receive NodeBuildComplete.");

            // Sidecar TaskHost will persist after the build is done.
            if (_nodeReuse)
            {
                _shutdownReason = NodeEngineShutdownReason.BuildCompleteReuse;
            }
            else
            {
                _shutdownReason = buildComplete.PrepareForReuse && Traits.Instance.EscapeHatches.ReuseTaskHostNodes ? NodeEngineShutdownReason.BuildCompleteReuse : NodeEngineShutdownReason.BuildComplete;
            }
            _shutdownEvent.Set();
        }

        /// <summary>
        /// Perform necessary actions to shut down the node.
        /// </summary>
        private NodeEngineShutdownReason HandleShutdown()
        {
            _taskRunnerThread?.Join();

            using StreamWriter debugWriter = _debugCommunications
                    ? File.CreateText(string.Format(CultureInfo.CurrentCulture, Path.Combine(FileUtilities.TempFileDirectory, @"MSBuild_NodeShutdown_{0}.txt"), EnvironmentUtilities.CurrentProcessId))
                    : null;

            debugWriter?.WriteLine("Node shutting down with reason {0}.", _shutdownReason);

#if !CLR2COMPATIBILITY
            _registeredTaskObjectCache.DisposeCacheObjects(RegisteredTaskObjectLifetime.Build);
            _registeredTaskObjectCache = null;
#endif

            NativeMethodsShared.SetCurrentDirectory(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);

            try
            {
                CommunicationsUtilities.SetEnvironment(_savedEnvironment);
            }
            catch (Exception ex)
            {
                debugWriter?.WriteLine("Failed to restore the original environment: {0}.", ex);
            }

            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(new NodeShutdown(_shutdownReason == NodeEngineShutdownReason.Error ? NodeShutdownReason.Error : NodeShutdownReason.Requested));
                _nodeEndpoint.OnLinkStatusChanged -= new LinkStatusChangedDelegate(OnLinkStatusChanged);
            }

            _nodeEndpoint.Disconnect();

#if CLR2COMPATIBILITY
            _packetReceivedEvent.Close();
            _shutdownEvent.Close();
            _taskCompleteEvent.Close();
            _taskCancelledEvent.Close();
#else
            _packetReceivedEvent.Dispose();
            _shutdownEvent.Dispose();
            _taskCompleteEvent.Dispose();
            _taskCancelledEvent.Dispose();
#endif

            return _shutdownReason;
        }

        /// <summary>
        /// Event handler for the node endpoint's LinkStatusChanged event.
        /// </summary>
        private void OnLinkStatusChanged(INodeEndpoint endpoint, LinkStatus status)
        {
            switch (status)
            {
                case LinkStatus.ConnectionFailed:
                case LinkStatus.Failed:
                    _shutdownReason = NodeEngineShutdownReason.ConnectionFailed;
                    _shutdownEvent.Set();
                    break;

                case LinkStatus.Inactive:
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Task runner method
        /// </summary>
        private void RunTask(object state)
        {
            _isTaskExecuting = true;
            OutOfProcTaskHostTaskResult taskResult = null;
            TaskHostConfiguration taskConfiguration = state as TaskHostConfiguration;
            IDictionary<string, TaskParameter> taskParams = taskConfiguration.TaskParameters;

            _debugCommunications = taskConfiguration.BuildProcessEnvironment.ContainsValueAndIsEqual("MSBUILDDEBUGCOMM", "1", StringComparison.OrdinalIgnoreCase);
            _updateEnvironment = !taskConfiguration.BuildProcessEnvironment.ContainsValueAndIsEqual("MSBuildTaskHostDoNotUpdateEnvironment", "1", StringComparison.OrdinalIgnoreCase);
            _updateEnvironmentAndLog = taskConfiguration.BuildProcessEnvironment.ContainsValueAndIsEqual("MSBuildTaskHostUpdateEnvironmentAndLog", "1", StringComparison.OrdinalIgnoreCase);
            WarningsAsErrors = taskConfiguration.WarningsAsErrors;
            WarningsNotAsErrors = taskConfiguration.WarningsNotAsErrors;
            WarningsAsMessages = taskConfiguration.WarningsAsMessages;
            try
            {
                NativeMethodsShared.SetCurrentDirectory(taskConfiguration.StartupDirectory);

                if (_updateEnvironment)
                {
                    InitializeMismatchedEnvironmentTable(taskConfiguration.BuildProcessEnvironment);
                }

                SetTaskHostEnvironment(taskConfiguration.BuildProcessEnvironment);

                Thread.CurrentThread.CurrentCulture = taskConfiguration.Culture;
                Thread.CurrentThread.CurrentUICulture = taskConfiguration.UICulture;

                string taskName = taskConfiguration.TaskName;
                string taskLocation = taskConfiguration.TaskLocation;
#if !CLR2COMPATIBILITY
                TaskFactoryUtilities.RegisterAssemblyResolveHandlersFromManifest(taskLocation);
#endif
                _taskWrapper = new OutOfProcTaskAppDomainWrapper();

                taskResult = _taskWrapper.ExecuteTask(
                    this as IBuildEngine,
                    taskName,
                    taskLocation,
                    taskConfiguration.ProjectFileOfTask,
                    taskConfiguration.LineNumberOfTask,
                    taskConfiguration.ColumnNumberOfTask,
                    taskConfiguration.TargetName,
                    taskConfiguration.ProjectFile,
#if FEATURE_APPDOMAIN
                    taskConfiguration.AppDomainSetup,
#endif
#if !NET35
                    taskConfiguration.HostServices,
#endif
                    taskParams);
            }
            catch (ThreadAbortException)
            {
                taskResult = new OutOfProcTaskHostTaskResult(TaskCompleteType.Failure);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                taskResult = new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedDuringExecution, e);
            }
            finally
            {
                try
                {
                    _isTaskExecuting = false;

                    IDictionary<string, string> currentEnvironment = CommunicationsUtilities.GetEnvironmentVariables();
                    currentEnvironment = UpdateEnvironmentForMainNode(currentEnvironment);

                    taskResult ??= new OutOfProcTaskHostTaskResult(TaskCompleteType.Failure);

                    lock (_taskCompleteLock)
                    {
                        _taskCompletePacket = new TaskHostTaskComplete(
                            taskResult,
#if FEATURE_REPORTFILEACCESSES
                            _fileAccessData,
#endif
                            currentEnvironment);
                    }

#if FEATURE_APPDOMAIN
                    foreach (TaskParameter param in taskParams.Values)
                    {
                        RemotingServices.Disconnect(param);
                    }
#endif

                    CommunicationsUtilities.SetEnvironment(_savedEnvironment);
                }
                catch (Exception e)
                {
                    lock (_taskCompleteLock)
                    {
                        _taskCompletePacket = new TaskHostTaskComplete(
                            new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedAfterExecution, e),
#if FEATURE_REPORTFILEACCESSES
                            _fileAccessData,
#endif
                            null);
                    }
                }
                finally
                {
#if FEATURE_REPORTFILEACCESSES
                    _fileAccessData = new List<FileAccessData>();
#endif

                    _taskWrapper.CleanupTask();
                    _taskCompleteEvent.Set();
                }
            }
        }

        /// <summary>
        /// Set the environment for the task host.
        /// </summary>
        private void SetTaskHostEnvironment(IDictionary<string, string> environment)
        {
            ErrorUtilities.VerifyThrowInternalNull(s_mismatchedEnvironmentValues, "mismatchedEnvironmentValues");
            IDictionary<string, string> updatedEnvironment = null;

            if (_updateEnvironment)
            {
                foreach (KeyValuePair<string, KeyValuePair<string, string>> variable in s_mismatchedEnvironmentValues)
                {
                    string oldValue = variable.Value.Key;
                    string newValue = variable.Value.Value;

                    string environmentValue = null;
                    environment.TryGetValue(variable.Key, out environmentValue);

                    if (String.Equals(environmentValue, oldValue, StringComparison.OrdinalIgnoreCase))
                    {
                        if (updatedEnvironment == null)
                        {
                            if (_updateEnvironmentAndLog)
                            {
                                LogMessageFromResource(MessageImportance.Low, "ModifyingTaskHostEnvironmentHeader");
                            }

                            updatedEnvironment = new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase);
                        }

                        if (newValue != null)
                        {
                            if (_updateEnvironmentAndLog)
                            {
                                LogMessageFromResource(MessageImportance.Low, "ModifyingTaskHostEnvironmentVariable", variable.Key, newValue, environmentValue ?? String.Empty);
                            }

                            updatedEnvironment[variable.Key] = newValue;
                        }
                        else
                        {
                            updatedEnvironment.Remove(variable.Key);
                        }
                    }
                }
            }

            if (updatedEnvironment == null)
            {
                updatedEnvironment = environment;
            }

            CommunicationsUtilities.SetEnvironment(updatedEnvironment);
        }

        /// <summary>
        /// Given the environment of the task host at the end of task execution, make sure that any
        /// processor-specific variables have been re-applied in the correct form for the main node.
        /// </summary>
        private IDictionary<string, string> UpdateEnvironmentForMainNode(IDictionary<string, string> environment)
        {
            ErrorUtilities.VerifyThrowInternalNull(s_mismatchedEnvironmentValues, "mismatchedEnvironmentValues");
            IDictionary<string, string> updatedEnvironment = null;

            if (_updateEnvironment)
            {
                foreach (KeyValuePair<string, KeyValuePair<string, string>> variable in s_mismatchedEnvironmentValues)
                {
                    string oldValue = variable.Value.Value;
                    string newValue = variable.Value.Key;

                    string environmentValue = null;
                    environment.TryGetValue(variable.Key, out environmentValue);

                    if (String.Equals(environmentValue, oldValue, StringComparison.OrdinalIgnoreCase))
                    {
                        updatedEnvironment ??= new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase);

                        if (newValue != null)
                        {
                            updatedEnvironment[variable.Key] = newValue;
                        }
                        else
                        {
                            updatedEnvironment.Remove(variable.Key);
                        }
                    }
                }
            }

            if (updatedEnvironment == null)
            {
                updatedEnvironment = environment;
            }

            return updatedEnvironment;
        }

        /// <summary>
        /// Make sure the mismatchedEnvironmentValues table has been populated.
        /// </summary>
        private void InitializeMismatchedEnvironmentTable(IDictionary<string, string> environment)
        {
            if (s_mismatchedEnvironmentValues == null)
            {
                s_mismatchedEnvironmentValues = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, string> variable in _savedEnvironment)
                {
                    string oldValue = variable.Value;
                    string newValue;
                    if (!environment.TryGetValue(variable.Key, out newValue))
                    {
                        s_mismatchedEnvironmentValues[variable.Key] = new KeyValuePair<string, string>(null, oldValue);
                    }
                    else
                    {
                        if (!String.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
                        {
                            s_mismatchedEnvironmentValues[variable.Key] = new KeyValuePair<string, string>(newValue, oldValue);
                        }
                    }
                }

                foreach (KeyValuePair<string, string> variable in environment)
                {
                    string newValue = variable.Value;
                    string oldValue;
                    if (!_savedEnvironment.TryGetValue(variable.Key, out oldValue))
                    {
                        s_mismatchedEnvironmentValues[variable.Key] = new KeyValuePair<string, string>(newValue, null);
                    }
                    else
                    {
                        if (!String.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
                        {
                            s_mismatchedEnvironmentValues[variable.Key] = new KeyValuePair<string, string>(newValue, oldValue);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sends the requested packet across to the main node.
        /// </summary>
        protected void SendBuildEvent(BuildEventArgs e)
        {
            if (_nodeEndpoint?.LinkStatus == LinkStatus.Active)
            {
#pragma warning disable SYSLIB0050
                if (!e.GetType().GetTypeInfo().IsSerializable && e is not IExtendedBuildEventArgs)
#pragma warning disable SYSLIB0050
                {
                    LogWarningFromResource("ExpectedEventToBeSerializable", e.GetType().Name);
                    return;
                }

                LogMessagePacketBase logMessage = new(new KeyValuePair<int, BuildEventArgs>(_currentConfiguration.NodeId, e));
                _nodeEndpoint.SendData(logMessage);
            }
        }

        /// <summary>
        /// Generates the message event corresponding to a particular resource string and set of args
        /// </summary>
        protected void LogMessageFromResource(MessageImportance importance, string messageResource, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration when we're trying to log messages!");

            BuildMessageEventArgs message = new BuildMessageEventArgs(
                                                    ResourceUtilities.FormatString(AssemblyResources.GetString(messageResource), messageArgs),
                                                    null,
                                                    _currentConfiguration.TaskName,
                                                    importance);

            LogMessageEvent(message);
        }

        /// <summary>
        /// Generates the warning event corresponding to a particular resource string and set of args
        /// </summary>
        protected void LogWarningFromResource(string messageResource, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration when we're trying to log warnings!");

            BuildWarningEventArgs warning = new BuildWarningEventArgs(
                                                    null,
                                                    null,
                                                    ProjectFileOfTaskNode,
                                                    LineNumberOfTaskNode,
                                                    ColumnNumberOfTaskNode,
                                                    0,
                                                    0,
                                                    ResourceUtilities.FormatString(AssemblyResources.GetString(messageResource), messageArgs),
                                                    null,
                                                    _currentConfiguration.TaskName);

            LogWarningEvent(warning);
        }

        /// <summary>
        /// Generates the error event corresponding to a particular resource string
        /// </summary>
        protected void LogErrorFromResource(string messageResource)
        {
            ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration when we're trying to log errors!");

            BuildErrorEventArgs error = new BuildErrorEventArgs(
                                                    null,
                                                    null,
                                                    ProjectFileOfTaskNode,
                                                    LineNumberOfTaskNode,
                                                    ColumnNumberOfTaskNode,
                                                    0,
                                                    0,
                                                    AssemblyResources.GetString(messageResource),
                                                    null,
                                                    _currentConfiguration.TaskName);

            LogErrorEvent(error);
        }
    }
}
