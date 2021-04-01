// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

#if FEATURE_APPDOMAIN
using System.Runtime.Remoting;
#endif

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This class represents an implementation of INode for RAR service out-of-proc node for hosting 'resolve assembly reference' tasks.
    /// </summary>
    internal class RarServiceTaskHostNode :
#if FEATURE_APPDOMAIN
        MarshalByRefObject, 
#endif
        INodePacketFactory, INodePacketHandler,
#if CLR2COMPATIBILITY
        IBuildEngine3
#else
        IBuildEngine7
#endif
    {
        /// <summary>
        /// backing queue used to control creating concurrent RAR service clients (requests)
        /// </summary>
        private static BlockingCollection<bool> s_waitForConcurrentClient = new ();

        /// <summary>
        /// The endpoint used to talk to the host.
        /// </summary>
        private NodeEndpointRarTaskHost _nodeEndpoint;

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
        private TaskHostConfiguration _currentConfiguration;

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
        private bool _isTaskExecuting;

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
        private Object _taskCompleteLock = new Object();

        /// <summary>
        /// The event which is set when a task is cancelled
        /// </summary>
        private ManualResetEvent _taskCancelledEvent;

        /// <summary>
        /// The thread currently executing user task in the TaskRunner
        /// </summary>
        private Thread _taskRunnerThread;

        /// <summary>
        /// This is the wrapper for the user task to be executed.
        /// We are providing a wrapper to create a possibility of executing the task in a separate AppDomain
        /// </summary>
        private OutOfProcTaskAppDomainWrapper _taskWrapper;

#if !CLR2COMPATIBILITY
        /// <summary>
        /// The task object cache.
        /// </summary>
        private RegisteredTaskObjectCacheBase _registeredTaskObjectCache;
#endif

        /// <summary>
        /// unique id of this class instance
        /// </summary>
        private Guid _id;

        /// <summary>
        /// Constructor.
        /// </summary>
        public RarServiceTaskHostNode()
        {
            _id = Guid.NewGuid();

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
        /// Stub implementation of IBuildEngine2.IsRunningMultipleNodes.  The task host does not support this sort of 
        /// IBuildEngine callback, so error. 
        /// </summary>
        public bool IsRunningMultipleNodes
        {
            get
            {
                LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
                return false;
            }
        }

        #endregion // IBuildEngine2 Implementation (Properties)

        #region IBuildEngine7 Implementation
        /// <summary>
        /// Enables or disables emitting a default error when a task fails without logging errors
        /// </summary>
        public bool AllowFailureWithoutError { get; set; } = false;
        #endregion

        #region IBuildEngine Implementation (Methods)

        /// <summary>
        /// Sends the provided error back to the parent node to be logged, tagging it with 
        /// the parent node's ID so that, as far as anyone is concerned, it might as well have 
        /// just come from the parent node to begin with. 
        /// </summary>
        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            SendBuildEvent(e);
        }

        /// <summary>
        /// Sends the provided warning back to the parent node to be logged, tagging it with 
        /// the parent node's ID so that, as far as anyone is concerned, it might as well have 
        /// just come from the parent node to begin with. 
        /// </summary>
        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            SendBuildEvent(e);
        }

        /// <summary>
        /// Sends the provided message back to the parent node to be logged, tagging it with 
        /// the parent node's ID so that, as far as anyone is concerned, it might as well have 
        /// just come from the parent node to begin with. 
        /// </summary>
        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            SendBuildEvent(e);
        }

        /// <summary>
        /// Sends the provided custom event back to the parent node to be logged, tagging it with 
        /// the parent node's ID so that, as far as anyone is concerned, it might as well have 
        /// just come from the parent node to begin with. 
        /// </summary>
        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            SendBuildEvent(e);
        }

        /// <summary>
        /// Stub implementation of IBuildEngine.BuildProjectFile.  The task host does not support IBuildEngine 
        /// callbacks for the purposes of building projects, so error.  
        /// </summary>
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
            return false;
        }

        #endregion // IBuildEngine Implementation (Methods)

        #region IBuildEngine2 Implementation (Methods)

        /// <summary>
        /// Stub implementation of IBuildEngine2.BuildProjectFile.  The task host does not support IBuildEngine 
        /// callbacks for the purposes of building projects, so error.  
        /// </summary>
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
        {
            LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
            return false;
        }

        /// <summary>
        /// Stub implementation of IBuildEngine2.BuildProjectFilesInParallel.  The task host does not support IBuildEngine 
        /// callbacks for the purposes of building projects, so error.  
        /// </summary>
        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
        {
            LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
            return false;
        }

        #endregion // IBuildEngine2 Implementation (Methods)

        #region IBuildEngine3 Implementation

        /// <summary>
        /// Stub implementation of IBuildEngine3.BuildProjectFilesInParallel.  The task host does not support IBuildEngine 
        /// callbacks for the purposes of building projects, so error.  
        /// </summary>
        public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
        {
            LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
            return new BuildEngineResult(false, null);
        }

        /// <summary>
        /// Stub implementation of IBuildEngine3.Yield.  The task host does not support yielding, so just go ahead and silently
        /// return, letting the task continue. 
        /// </summary>
        public void Yield()
        {
            return;
        }

        /// <summary>
        /// Stub implementation of IBuildEngine3.Reacquire. The task host does not support yielding, so just go ahead and silently 
        /// return, letting the task continue. 
        /// </summary>
        public void Reacquire()
        {
            return;
        }

        #endregion // IBuildEngine3 Implementation

#if !CLR2COMPATIBILITY
        #region IBuildEngine4 Implementation

        /// <summary>
        /// Registers an object with the system that will be disposed of at some specified time
        /// in the future.
        /// </summary>
        /// <param name="key">The key used to retrieve the object.</param>
        /// <param name="obj">The object to be held for later disposal.</param>
        /// <param name="lifetime">The lifetime of the object.</param>
        /// <param name="allowEarlyCollection">The object may be disposed earlier that the requested time if
        /// MSBuild needs to reclaim memory.</param>
        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            _registeredTaskObjectCache.RegisterTaskObject(key, obj, lifetime, allowEarlyCollection);
        }

        /// <summary>
        /// Retrieves a previously registered task object stored with the specified key.
        /// </summary>
        /// <param name="key">The key used to retrieve the object.</param>
        /// <param name="lifetime">The lifetime of the object.</param>
        /// <returns>
        /// The registered object, or null is there is no object registered under that key or the object
        /// has been discarded through early collection.
        /// </returns>
        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            return _registeredTaskObjectCache.GetRegisteredTaskObject(key, lifetime);
        }

        /// <summary>
        /// Unregisters a previously-registered task object.
        /// </summary>
        /// <param name="key">The key used to retrieve the object.</param>
        /// <param name="lifetime">The lifetime of the object.</param>
        /// <returns>
        /// The registered object, or null is there is no object registered under that key or the object
        /// has been discarded through early collection.
        /// </returns>
        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            return _registeredTaskObjectCache.UnregisterTaskObject(key, lifetime);
        }

        #endregion

        #region IBuildEngine5 Implementation

        /// <summary>
        /// Logs a telemetry event.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        /// <param name="properties">The list of properties associated with the event.</param>
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
        /// <returns>An <see cref="IReadOnlyDictionary{String, String}" /> containing the global properties of the current project.</returns>
        public IReadOnlyDictionary<string, string> GetGlobalProperties()
        {
            return new Dictionary<string, string>(_currentConfiguration.GlobalProperties);
        }

        #endregion
#endif

        #region INodePacketFactory Members

        /// <summary>
        /// Registers the specified handler for a particular packet type.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="factory">The factory for packets of the specified type.</param>
        /// <param name="handler">The handler to be called when packets of the specified type are received.</param>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Takes a serializer, deserializes the packet and routes it to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            _packetFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Routes the specified packet
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            _packetFactory.RoutePacket(nodeId, packet);
        }

        #endregion // INodePacketFactory Members

        #region INodePacketHandler Members

        /// <summary>
        /// This method is invoked by the NodePacketRouter when a packet is received and is intended for
        /// this recipient.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
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

        public static NodeEngineShutdownReason StartRarService(string pipeName, out Exception shutdownException)
        {
            CommunicationsUtilities.Trace("RAR node starting.");
            PipeName = pipeName;

            // Grab the service  mutex to prevent multiple RAR services from starting with the same
            // pipename, consuming excess resources and lesser cache hit ratio.
            // If someone else holds the mutex exit immediately with a non-zero exit code.
            var mutexName = GetRarServiceMutexName(pipeName);
            using var serverMutex = CommunicationsUtilities.OpenOrCreateMutex(name: mutexName, createdNew: out var createdNew);
            if (!createdNew)
            {
                shutdownException = new InvalidOperationException($"RAR service for pipename '{pipeName}' is already running.");
                CommunicationsUtilities.Trace(shutdownException.ToString());
                return NodeEngineShutdownReason.Error;
            }

            return StartRarServiceInternal(out shutdownException);
        }

        private static string GetRarServiceMutexName(string pipeName) => $"{pipeName}.server";

        private static NodeEngineShutdownReason StartRarServiceInternal(out Exception shutdownException)
        {
            const int minListenCapacity = 3;
            int currentCapacity = 0;

            // initiate with two waiting clients
            for (int i = 0; i < minListenCapacity; i++)
            {
                s_waitForConcurrentClient.Add(true);
            }

            // run draining queue
            foreach (var _ in s_waitForConcurrentClient.GetConsumingEnumerable())
            {
                // rework into threads? or long running tasks?
                System.Threading.Tasks.Task.Run(() =>
                {
                    // TODO: block when max clients is reached

                    Interlocked.Increment(ref currentCapacity);

                    var rarTaskHost = new RarServiceTaskHostNode();
                    CommunicationsUtilities.Trace("RAR node client {0} started to wait for task. Currently running instances {1}", rarTaskHost._id, currentCapacity);
                    var shutdownReason = rarTaskHost.Run(out var rarTaskHostShutdownException);
                    CommunicationsUtilities.Trace("RAR node client {0} finished with shutdownReason: {1} and exception: '{2}'.", rarTaskHost._id, shutdownReason,
                        rarTaskHostShutdownException);

                    Interlocked.Decrement(ref currentCapacity);
                    // TODO: somehow react to shutdownException and unexpected shutdownReason, maybe log unexpected somewhere?
                });
            }

            // TODO: how to do graceful node termination and automatic inactivity timeout termination?
            throw new NotImplementedException("It shall never get here, as cancellation is not implemented yet");
        }

        /// <summary>
        /// Current process wide pine name used for 'named pipe' communication
        /// </summary>
        public static string PipeName { get; set; }

        /// <summary>
        /// Starts up the node and processes messages until the node is requested to shut down.
        /// </summary>
        /// <param name="isRar">True if we are hosting RAR node</param>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <returns>The reason for shutting down.</returns>
        public NodeEngineShutdownReason Run(out Exception shutdownException)
        {
#if !CLR2COMPATIBILITY
            _registeredTaskObjectCache = new RegisteredTaskObjectCacheBase();
#endif
            shutdownException = null;

            _nodeEndpoint = new NodeEndpointRarTaskHost(PipeName);
            _nodeEndpoint.OnLinkStatusChanged += new LinkStatusChangedDelegate(OnLinkStatusChanged);
            _nodeEndpoint.Listen(this);

            CommunicationsUtilities.Trace($"Listening RAR node client {this.GetHashCode()}");

            WaitHandle[] waitHandles = new WaitHandle[] { _shutdownEvent, _packetReceivedEvent, _taskCompleteEvent, _taskCancelledEvent };

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
        /// Dispatches the packet to the correct handler.
        /// </summary>
        private void HandlePacket(INodePacket packet)
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
        /// Configure the task host according to the information received in the 
        /// configuration packet
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
            // If so, now that we've completed the task, we want to shut down 
            // this node -- with no reuse, since we don't know whether the 
            // task we canceled left the node in a good state or not. 
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
            // RAR task do not support cancellation
        }

        /// <summary>
        /// Handles the NodeBuildComplete packet.
        /// </summary>
        private void HandleNodeBuildComplete(NodeBuildComplete buildComplete)
        {
            CommunicationsUtilities.Trace($"RAR node client {this.GetHashCode()} handling NodeBuildComplete");
            ErrorUtilities.VerifyThrow(!_isTaskExecuting, "We should never have a task in the process of executing when we receive NodeBuildComplete.");

            _shutdownReason = buildComplete.PrepareForReuse ? NodeEngineShutdownReason.BuildCompleteReuse : NodeEngineShutdownReason.BuildComplete;
            _shutdownEvent.Set();
        }

        /// <summary>
        /// Perform necessary actions to shut down the node.
        /// </summary>
        private NodeEngineShutdownReason HandleShutdown()
        {
            // Wait for the RunTask task runner thread before shutting down so that we can cleanly dispose all WaitHandles.
            _taskRunnerThread?.Join();

            CommunicationsUtilities.Trace($"RAR node client {this.GetHashCode()} shutting down with reason {_shutdownReason}");

#if !CLR2COMPATIBILITY
            _registeredTaskObjectCache.DisposeCacheObjects(RegisteredTaskObjectLifetime.Build);
            _registeredTaskObjectCache = null;
#endif

            // On Windows, a process holds a handle to the current directory,
            // so reset it away from a user-requested folder that may get deleted.
            NativeMethodsShared.SetCurrentDirectory(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);

            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                // Notify the BuildManager that we are done.
                _nodeEndpoint.SendData(new NodeShutdown(_shutdownReason == NodeEngineShutdownReason.Error ? NodeShutdownReason.Error : NodeShutdownReason.Requested));

                // Flush all packets to the pipe and close it down.  This blocks until the shutdown is complete.
                _nodeEndpoint.OnLinkStatusChanged -= new LinkStatusChangedDelegate(OnLinkStatusChanged);
            }

            _nodeEndpoint.Disconnect();

            // Dispose these WaitHandles
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
                    // connection fail - unexpected but could happen if handshake fails for any reason
                    // in such case we need to start new client as LinkStatus.Active has not been reached
                    CommunicationsUtilities.Trace("RAR {0} failed to connect to client or timeouted. Link Failed. New RAR client requested.", _id);
                    s_waitForConcurrentClient.Add(false);

                    _shutdownReason = NodeEngineShutdownReason.ConnectionFailed;
                    _shutdownEvent.Set();
                    break;

                case LinkStatus.Inactive:
                    break;

                case LinkStatus.Active:
                    // start listen to another incoming connection
                    CommunicationsUtilities.Trace("RAR node {0} connected to client. Link Active. New RAR client requested.", _id);
                    s_waitForConcurrentClient.Add(true);
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

            CommunicationsUtilities.Trace("RAR node {0} starting task {1} {2}.", _id, taskConfiguration.TaskName, taskConfiguration.NodeId);

            try
            {
                // Set culture
                Thread.CurrentThread.CurrentCulture = taskConfiguration.Culture;
                Thread.CurrentThread.CurrentUICulture = taskConfiguration.UICulture;

                string taskName = taskConfiguration.TaskName;
                string taskLocation = taskConfiguration.TaskLocation;

                // We will not create an appdomain now because of a bug
                // As a fix, we will create the class directly without wrapping it in a domain
                _taskWrapper = new OutOfProcTaskAppDomainWrapper();
                var taskExecutionContext = new TaskExecutionContext(taskConfiguration.StartupDirectory, taskConfiguration.BuildProcessEnvironment, taskConfiguration.Culture, taskConfiguration.UICulture);

                taskResult = _taskWrapper.ExecuteTask
                (
                    this as IBuildEngine,
                    taskName,
                    taskLocation,
                    taskConfiguration.ProjectFileOfTask,
                    taskConfiguration.LineNumberOfTask,
                    taskConfiguration.ColumnNumberOfTask,
#if FEATURE_APPDOMAIN
                    taskConfiguration.AppDomainSetup,
#endif
                    taskParams,
                    taskExecutionContext
                );
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException)
                {
                    // This thread was aborted as part of Cancellation, we will return a failure task result
                    taskResult = new OutOfProcTaskHostTaskResult(TaskCompleteType.Failure);
                }
                else
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }
                else
                {
                    taskResult = new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedDuringExecution, e);
                }
            }
            finally
            {
                try
                {
                    _isTaskExecuting = false;

                    if (taskResult == null)
                    {
                        taskResult = new OutOfProcTaskHostTaskResult(TaskCompleteType.Failure);
                    }

                    lock (_taskCompleteLock)
                    {
                        _taskCompletePacket = new TaskHostTaskComplete
                                                    (
                                                        taskResult,
                                                        null
                                                    );
                    }

#if FEATURE_APPDOMAIN
                    foreach (TaskParameter param in taskParams.Values)
                    {
                        // Tell remoting to forget connections to the parameter
                        RemotingServices.Disconnect(param);
                    }
#endif
                }
                catch (Exception e)
                {
                    lock (_taskCompleteLock)
                    {
                        // Create a minimal taskCompletePacket to carry the exception so that the TaskHostTask does not hang while waiting
                        _taskCompletePacket = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedAfterExecution, e), null);
                    }
                }
                finally
                {
                    // Call CleanupTask to unload any domains and other necessary cleanup in the taskWrapper
                    _taskWrapper.CleanupTask();

                    // The task has now fully completed executing
                    _taskCompleteEvent.Set();

                    CommunicationsUtilities.Trace("RAR node {0} finished task {1} {2}.", _id, taskConfiguration.TaskName, taskConfiguration.NodeId);
                }
            }
        }

        /// <summary>
        /// Sends the requested packet across to the main node. 
        /// </summary>
        private void SendBuildEvent(BuildEventArgs e)
        {
            if (_nodeEndpoint?.LinkStatus == LinkStatus.Active)
            {
                if (!e.GetType().GetTypeInfo().IsSerializable)
                {
                    // log a warning and bail.  This will end up re-calling SendBuildEvent, but we know for a fact
                    // that the warning that we constructed is serializable, so everything should be good.  
                    LogWarningFromResource("ExpectedEventToBeSerializable", e.GetType().Name);
                    return;
                }

                _nodeEndpoint.SendData(new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(_currentConfiguration.NodeId, e)));
            }
        }

        /// <summary>
        /// Generates the message event corresponding to a particular resource string and set of args
        /// </summary>
        private void LogMessageFromResource(MessageImportance importance, string messageResource, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration when we're trying to log messages!");

            // Using the CLR 2 build event because this class is shared between MSBuildTaskHost.exe (CLR2) and MSBuild.exe (CLR4+)
            BuildMessageEventArgs message = new BuildMessageEventArgs
                                                (
                                                    ResourceUtilities.FormatString(AssemblyResources.GetString(messageResource), messageArgs),
                                                    null,
                                                    _currentConfiguration.TaskName,
                                                    importance
                                                );

            LogMessageEvent(message);
        }

        /// <summary>
        /// Generates the error event corresponding to a particular resource string and set of args
        /// </summary>
        private void LogWarningFromResource(string messageResource, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration when we're trying to log warnings!");

            // Using the CLR 2 build event because this class is shared between MSBuildTaskHost.exe (CLR2) and MSBuild.exe (CLR4+)
            BuildWarningEventArgs warning = new BuildWarningEventArgs
                                                (
                                                    null,
                                                    null,
                                                    ProjectFileOfTaskNode,
                                                    LineNumberOfTaskNode,
                                                    ColumnNumberOfTaskNode,
                                                    0,
                                                    0,
                                                    ResourceUtilities.FormatString(AssemblyResources.GetString(messageResource), messageArgs),
                                                    null,
                                                    _currentConfiguration.TaskName
                                                );

            LogWarningEvent(warning);
        }

        /// <summary>
        /// Generates the error event corresponding to a particular resource string and set of args
        /// </summary>
        private void LogErrorFromResource(string messageResource)
        {
            ErrorUtilities.VerifyThrow(_currentConfiguration != null, "We should never have a null configuration when we're trying to log errors!");

            // Using the CLR 2 build event because this class is shared between MSBuildTaskHost.exe (CLR2) and MSBuild.exe (CLR4+)
            BuildErrorEventArgs error = new BuildErrorEventArgs
                                                (
                                                    null,
                                                    null,
                                                    ProjectFileOfTaskNode,
                                                    LineNumberOfTaskNode,
                                                    ColumnNumberOfTaskNode,
                                                    0,
                                                    0,
                                                    AssemblyResources.GetString(messageResource),
                                                    null,
                                                    _currentConfiguration.TaskName
                                                );

            LogErrorEvent(error);
        }
    }
}
