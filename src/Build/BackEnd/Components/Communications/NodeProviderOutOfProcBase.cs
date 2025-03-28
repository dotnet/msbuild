// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains the shared pieces of code from NodeProviderOutOfProc
    /// and NodeProviderOutOfProcTaskHost.
    /// </summary>
    internal abstract class NodeProviderOutOfProcBase
    {
        /// <summary>
        /// The number of times to retry creating an out-of-proc node.
        /// </summary>
        private const int NodeCreationRetries = 10;

        /// <summary>
        /// The amount of time to wait for an out-of-proc node to spool up before we give up.
        /// </summary>
        private const int TimeoutForNewNodeCreation = 30000;

        /// <summary>
        /// The amount of time to wait for an out-of-proc node to exit.
        /// </summary>
        private const int TimeoutForWaitForExit = 30000;

        /// <summary>
        /// The build component host.
        /// </summary>
        private IBuildComponentHost _componentHost;

        /// <summary>
        /// Keeps track of the processes we've already checked for nodes so we don't check them again.
        /// We decided to use ConcurrentDictionary of(string, byte) as common implementation of ConcurrentHashSet.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte /*void*/> _processesToIgnore = new();

        /// <summary>
        /// Delegate used to tell the node provider that a context has been created.
        /// </summary>
        /// <param name="context">The created node context.</param>
        internal delegate void NodeContextCreatedDelegate(NodeContext context);

        /// <summary>
        /// Delegate used to tell the node provider that a context has terminated.
        /// </summary>
        /// <param name="nodeId">The id of the node which terminated.</param>
        internal delegate void NodeContextTerminateDelegate(int nodeId);

        /// <summary>
        /// The build component host.
        /// </summary>
        protected IBuildComponentHost ComponentHost
        {
            get { return _componentHost; }
            set { _componentHost = value; }
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="context">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        protected void SendData(NodeContext context, INodePacket packet)
        {
            ErrorUtilities.VerifyThrowArgumentNull(packet);
            context.SendData(packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="contextsToShutDown">List of the contexts to be shut down</param>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        protected void ShutdownConnectedNodes(List<NodeContext> contextsToShutDown, bool enableReuse)
        {
            // Send the build completion message to the nodes, causing them to shutdown or reset.
            _processesToIgnore.Clear();

            // We wait for child nodes to exit to avoid them changing the terminal
            // after this process terminates.
            bool waitForExit = !enableReuse &&
                                !Console.IsInputRedirected &&
                                Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout;

            Task[] waitForExitTasks = waitForExit && contextsToShutDown.Count > 0 ? new Task[contextsToShutDown.Count] : null;
            int i = 0;
            var loggingService = _componentHost.LoggingService;
            foreach (NodeContext nodeContext in contextsToShutDown)
            {
                if (nodeContext is null)
                {
                    continue;
                }
                nodeContext.SendData(new NodeBuildComplete(enableReuse));
                if (waitForExit)
                {
                    waitForExitTasks[i++] = nodeContext.WaitForExitAsync(loggingService);
                }
            }
            if (waitForExitTasks != null)
            {
                Task.WaitAll(waitForExitTasks);
            }
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        /// <param name="nodeReuse">Whether to reuse the node</param>
        /// <param name="terminateNode">Delegate used to tell the node provider that a context has terminated</param>
        protected void ShutdownAllNodes(bool nodeReuse, NodeContextTerminateDelegate terminateNode)
        {
            // INodePacketFactory
            INodePacketFactory factory = new NodePacketFactory();

            List<Process> nodeProcesses = GetPossibleRunningNodes().nodeProcesses.ToList();

            // Find proper MSBuildTaskHost executable name
            string msbuildtaskhostExeName = NodeProviderOutOfProcTaskHost.TaskHostNameForClr2TaskHost;

            // Search for all instances of msbuildtaskhost process and add them to the process list
            nodeProcesses.AddRange(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(msbuildtaskhostExeName)));

            // For all processes in the list, send signal to terminate if able to connect
            foreach (Process nodeProcess in nodeProcesses)
            {
                // A 2013 comment suggested some nodes take this long to respond, so a smaller timeout would miss nodes.
                int timeout = 30;

                // Attempt to connect to the process with the handshake without low priority.
                NodePipeClient pipeClient = TryConnectToProcess(nodeProcess.Id, timeout, NodeProviderOutOfProc.GetHandshake(nodeReuse, false));

                // If we couldn't connect attempt to connect to the process with the handshake including low priority.
                pipeClient ??= TryConnectToProcess(nodeProcess.Id, timeout, NodeProviderOutOfProc.GetHandshake(nodeReuse, true));

                if (pipeClient != null)
                {
                    // If we're able to connect to such a process, send a packet requesting its termination
                    CommunicationsUtilities.Trace("Shutting down node with pid = {0}", nodeProcess.Id);
                    NodeContext nodeContext = new(0, nodeProcess, pipeClient, factory, terminateNode);
                    nodeContext.SendData(new NodeBuildComplete(false /* no node reuse */));
                    pipeClient.Dispose();
                }
            }
        }

        /// <summary>
        /// Finds or creates a child processes which can act as a node.
        /// </summary>
        protected IList<NodeContext> GetNodes(string msbuildLocation,
            string commandLineArgs,
            int nextNodeId,
            INodePacketFactory factory,
            Handshake hostHandshake,
            NodeContextCreatedDelegate createNode,
            NodeContextTerminateDelegate terminateNode,
            int numberOfNodesToCreate)
        {
#if DEBUG
            if (Execution.BuildManager.WaitForDebugger)
            {
                commandLineArgs += " /wfd";
            }
#endif

            if (String.IsNullOrEmpty(msbuildLocation))
            {
                msbuildLocation = _componentHost.BuildParameters.NodeExeLocation;
            }

            if (String.IsNullOrEmpty(msbuildLocation))
            {
                string msbuildExeName = Environment.GetEnvironmentVariable("MSBUILD_EXE_NAME");

                if (!String.IsNullOrEmpty(msbuildExeName))
                {
                    // we assume that MSBUILD_EXE_NAME is, in fact, just the name.
                    msbuildLocation = Path.Combine(msbuildExeName, ".exe");
                }
            }

            // Get all process of possible running node processes for reuse and put them into ConcurrentQueue.
            // Processes from this queue will be concurrently consumed by TryReusePossibleRunningNodes while
            //    trying to connect to them and reuse them. When queue is empty, no process to reuse left
            //    new node process will be started.
            string expectedProcessName = null;
            ConcurrentQueue<Process> possibleRunningNodes = null;
#if FEATURE_NODE_REUSE
            // Try to connect to idle nodes if node reuse is enabled.
            if (_componentHost.BuildParameters.EnableNodeReuse)
            {
                IList<Process> possibleRunningNodesList;
                (expectedProcessName, possibleRunningNodesList) = GetPossibleRunningNodes(msbuildLocation);
                possibleRunningNodes = new ConcurrentQueue<Process>(possibleRunningNodesList);

                if (possibleRunningNodesList.Count > 0)
                {
                    CommunicationsUtilities.Trace("Attempting to connect to {1} existing processes '{0}'...", expectedProcessName, possibleRunningNodesList.Count);
                }
            }
#endif
            ConcurrentQueue<NodeContext> nodeContexts = new();
            ConcurrentQueue<Exception> exceptions = new();
            int currentProcessId = EnvironmentUtilities.CurrentProcessId;
            Parallel.For(nextNodeId, nextNodeId + numberOfNodesToCreate, (nodeId) =>
            {
                try
                {
                    if (!TryReuseAnyFromPossibleRunningNodes(currentProcessId, nodeId) && !StartNewNode(nodeId))
                    {
                        // We were unable to reuse or launch a node.
                        CommunicationsUtilities.Trace("FAILED TO CONNECT TO A CHILD NODE");
                    }
                }
                catch (Exception ex)
                {
                    // It will be rethrown as aggregate exception
                    exceptions.Enqueue(ex);
                }
            });
            if (!exceptions.IsEmpty)
            {
                ErrorUtilities.ThrowInternalError("Cannot acquire required number of nodes.", new AggregateException(exceptions.ToArray()));
            }

            return nodeContexts.ToList();

            bool TryReuseAnyFromPossibleRunningNodes(int currentProcessId, int nodeId)
            {
                while (possibleRunningNodes != null && possibleRunningNodes.TryDequeue(out var nodeToReuse))
                {
                    CommunicationsUtilities.Trace("Trying to connect to existing process {2} with id {1} to establish node {0}...", nodeId, nodeToReuse.Id, nodeToReuse.ProcessName);
                    if (nodeToReuse.Id == currentProcessId)
                    {
                        continue;
                    }

                    // Get the full context of this inspection so that we can always skip this process when we have the same taskhost context
                    string nodeLookupKey = GetProcessesToIgnoreKey(hostHandshake, nodeToReuse.Id);
                    if (_processesToIgnore.ContainsKey(nodeLookupKey))
                    {
                        continue;
                    }

                    // We don't need to check this again
                    _processesToIgnore.TryAdd(nodeLookupKey, default);

                    // Attempt to connect to each process in turn.
                    NodePipeClient pipeClient = TryConnectToProcess(nodeToReuse.Id, 0 /* poll, don't wait for connections */, hostHandshake);
                    if (pipeClient != null)
                    {
                        // Connection successful, use this node.
                        CommunicationsUtilities.Trace("Successfully connected to existed node {0} which is PID {1}", nodeId, nodeToReuse.Id);
                        string msg = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("NodeReused", nodeId, nodeToReuse.Id);
                        _componentHost.LoggingService.LogBuildEvent(new BuildMessageEventArgs(msg, null, null, MessageImportance.Low)
                        {
                            BuildEventContext = new BuildEventContext(nodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId)
                        });

                        CreateNodeContext(nodeId, nodeToReuse, pipeClient);
                        return true;
                    }
                }

                return false;
            }

            // Create a new node process.
            bool StartNewNode(int nodeId)
            {
                CommunicationsUtilities.Trace("Could not connect to existing process, now creating a process...");

                // We try this in a loop because it is possible that there is another MSBuild multiproc
                // host process running somewhere which is also trying to create nodes right now.  It might
                // find our newly created node and connect to it before we get a chance.
                int retries = NodeCreationRetries;
                while (retries-- > 0)
                {
#if FEATURE_NET35_TASKHOST
                    // We will also check to see if .NET 3.5 is installed in the case where we need to launch a CLR2 OOP TaskHost.
                    // Failure to detect this has been known to stall builds when Windows pops up a related dialog.
                    // It's also a waste of time when we attempt several times to launch multiple MSBuildTaskHost.exe (CLR2 TaskHost)
                    // nodes because we should never be able to connect in this case.
                    string taskHostNameForClr2TaskHost = Path.GetFileNameWithoutExtension(NodeProviderOutOfProcTaskHost.TaskHostNameForClr2TaskHost);
                    if (Path.GetFileNameWithoutExtension(msbuildLocation).Equals(taskHostNameForClr2TaskHost, StringComparison.OrdinalIgnoreCase))
                    {
                        if (FrameworkLocationHelper.GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Current) == null)
                        {
                            CommunicationsUtilities.Trace(
                                "Failed to launch node from {0}. The required .NET Framework v3.5 is not installed or enabled. CommandLine: {1}",
                                msbuildLocation,
                                commandLineArgs);

                            string nodeFailedToLaunchError = ResourceUtilities.GetResourceString("TaskHostNodeFailedToLaunchErrorCodeNet35NotInstalled");
                            throw new NodeFailedToLaunchException(null, nodeFailedToLaunchError);
                        }
                    }
#endif
                    // Create the node process
                    INodeLauncher nodeLauncher = (INodeLauncher)_componentHost.GetComponent(BuildComponentType.NodeLauncher);
                    Process msbuildProcess = nodeLauncher.Start(msbuildLocation, commandLineArgs, nodeId);
                    _processesToIgnore.TryAdd(GetProcessesToIgnoreKey(hostHandshake, msbuildProcess.Id), default);

                    // Note, when running under IMAGEFILEEXECUTIONOPTIONS registry key to debug, the process ID
                    // gotten back from CreateProcess is that of the debugger, which causes this to try to connect
                    // to the debugger process. Instead, use MSBUILDDEBUGONSTART=1

                    // Now try to connect to it.
                    NodePipeClient pipeClient = TryConnectToProcess(msbuildProcess.Id, TimeoutForNewNodeCreation, hostHandshake);
                    if (pipeClient != null)
                    {
                        // Connection successful, use this node.
                        CommunicationsUtilities.Trace("Successfully connected to created node {0} which is PID {1}", nodeId, msbuildProcess.Id);

                        CreateNodeContext(nodeId, msbuildProcess, pipeClient);
                        return true;
                    }

                    if (msbuildProcess.HasExited)
                    {
                        if (Traits.Instance.DebugNodeCommunication)
                        {
                            try
                            {
                                CommunicationsUtilities.Trace("Could not connect to node with PID {0}; it has exited with exit code {1}. This can indicate a crash at startup", msbuildProcess.Id, msbuildProcess.ExitCode);
                            }
                            catch (InvalidOperationException)
                            {
                                // This case is common on Windows where we called CreateProcess and the Process object
                                // can't get the exit code.
                                CommunicationsUtilities.Trace("Could not connect to node with PID {0}; it has exited with unknown exit code. This can indicate a crash at startup", msbuildProcess.Id);
                            }
                        }
                    }
                    else
                    {
                        CommunicationsUtilities.Trace("Could not connect to node with PID {0}; it is still running. This can occur when two multiprocess builds run in parallel and the other one 'stole' this node", msbuildProcess.Id);
                    }
                }

                return false;
            }

            void CreateNodeContext(int nodeId, Process nodeToReuse, NodePipeClient pipeClient)
            {
                NodeContext nodeContext = new(nodeId, nodeToReuse, pipeClient, factory, terminateNode);
                nodeContexts.Enqueue(nodeContext);
                createNode(nodeContext);
            }
        }

        /// <summary>
        /// Finds processes named after either msbuild or msbuildtaskhost.
        /// </summary>
        /// <param name="msbuildLocation"></param>
        /// <returns>
        /// Item 1 is the name of the process being searched for.
        /// Item 2 is the ConcurrentQueue of ordered processes themselves.
        /// </returns>
        private (string expectedProcessName, IList<Process> nodeProcesses) GetPossibleRunningNodes(string msbuildLocation = null)
        {
            if (String.IsNullOrEmpty(msbuildLocation))
            {
                msbuildLocation = "MSBuild.exe";
            }

            var expectedProcessName = Path.GetFileNameWithoutExtension(CurrentHost.GetCurrentHost() ?? msbuildLocation);

            var processes = Process.GetProcessesByName(expectedProcessName);
            Array.Sort(processes, (left, right) => left.Id.CompareTo(right.Id));

            return (expectedProcessName, processes);
        }

        /// <summary>
        /// Generate a string from task host context and the remote process to be used as key to lookup processes we have already
        /// attempted to connect to or are already connected to
        /// </summary>
        private string GetProcessesToIgnoreKey(Handshake hostHandshake, int nodeProcessId)
        {
#if NET
            return string.Create(CultureInfo.InvariantCulture, $"{hostHandshake}|{nodeProcessId}");
#else
            return $"{hostHandshake}|{nodeProcessId.ToString(CultureInfo.InvariantCulture)}";
#endif
        }

        /// <summary>
        /// Attempts to connect to the specified process.
        /// </summary>
        private NodePipeClient TryConnectToProcess(int nodeProcessId, int timeout, Handshake handshake)
        {
            // Try and connect to the process.
            string pipeName = NamedPipeUtil.GetPlatformSpecificPipeName(nodeProcessId);

            NodePipeClient pipeClient = new(pipeName, handshake);

            CommunicationsUtilities.Trace("Attempting connect to PID {0}", nodeProcessId);

            try
            {
                pipeClient.ConnectToServer(timeout);
                return pipeClient;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // Can be:
                // UnauthorizedAccessException -- Couldn't connect, might not be a node.
                // IOException -- Couldn't connect, already in use.
                // TimeoutException -- Couldn't connect, might not be a node.
                // InvalidOperationException – Couldn’t connect, probably a different build
                CommunicationsUtilities.Trace("Failed to connect to pipe {0}. {1}", pipeName, e.Message.TrimEnd());

                // If we don't close any stream, we might hang up the child
                pipeClient?.Dispose();
            }

            return null;
        }

        /// <summary>
        /// Class which wraps up the communications infrastructure for a given node.
        /// </summary>
        internal class NodeContext
        {
            private enum ExitPacketState
            {
                None,
                ExitPacketQueued,
                ExitPacketSent
            }

            // The pipe client used to communicate with the node.
            private readonly NodePipeClient _pipeClient;

            /// <summary>
            /// The factory used to create packets from data read off the pipe.
            /// </summary>
            private readonly INodePacketFactory _packetFactory;

            /// <summary>
            /// The node id assigned by the node provider.
            /// </summary>
            private int _nodeId;

            /// <summary>
            /// The node process.
            /// </summary>
            private readonly Process _process;

            internal Process Process { get { return _process; } }

            /// <summary>
            /// A queue used for enqueuing packets to write to the stream asynchronously.
            /// </summary>
            private ConcurrentQueue<INodePacket> _packetWriteQueue = new ConcurrentQueue<INodePacket>();

            /// <summary>
            /// A task representing the last packet write, so we can chain packet writes one after another.
            /// We want to queue up writing packets on a separate thread asynchronously, but serially.
            /// Each task drains the <see cref="_packetWriteQueue"/>
            /// </summary>
            private Task _packetWriteDrainTask = Task.CompletedTask;

            /// <summary>
            /// Delegate called when the context terminates.
            /// </summary>
            private NodeContextTerminateDelegate _terminateDelegate;

            /// <summary>
            /// Tracks the state of the packet sent to terminate the node.
            /// </summary>
            private ExitPacketState _exitPacketState;

            /// <summary>
            /// Constructor.
            /// </summary>
            public NodeContext(int nodeId, Process process,
                NodePipeClient pipeClient,
                INodePacketFactory factory, NodeContextTerminateDelegate terminateDelegate)
            {
                _nodeId = nodeId;
                _process = process;
                _pipeClient = pipeClient;
                _pipeClient.RegisterPacketFactory(factory);
                _packetFactory = factory;
                _terminateDelegate = terminateDelegate;
            }

            /// <summary>
            /// Id of node.
            /// </summary>
            public int NodeId => _nodeId;

            /// <summary>
            /// Starts a new asynchronous read operation for this node.
            /// </summary>
            public void BeginAsyncPacketRead()
            {
                _ = ThreadPool.QueueUserWorkItem(_ => _ = RunPacketReadLoopAsync());
            }

            public async Task RunPacketReadLoopAsync()
            {
                INodePacket packet = null;

                while (packet?.Type != NodePacketType.NodeShutdown)
                {
                    try
                    {
                        packet = await _pipeClient.ReadPacketAsync().ConfigureAwait(false);
                    }
                    catch (IOException e)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "COMMUNICATIONS ERROR (HRC) Node: {0} Process: {1} Exception: {2}", _nodeId, _process.Id, e.Message);
                        packet = new NodeShutdown(NodeShutdownReason.ConnectionFailed);
                    }

                    if (packet.Type == NodePacketType.NodeShutdown && (packet as NodeShutdown).Reason == NodeShutdownReason.ConnectionFailed)
                    {
                        try
                        {
                            if (_process.HasExited)
                            {
                                CommunicationsUtilities.Trace(_nodeId, "   Child Process {0} has exited.", _process.Id);
                            }
                            else
                            {
                                CommunicationsUtilities.Trace(_nodeId, "   Child Process {0} is still running.", _process.Id);
                            }
                        }
                        catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                        {
                            CommunicationsUtilities.Trace(_nodeId, "Unable to retrieve remote process information. {0}", e);
                        }
                    }

                    _packetFactory.RoutePacket(_nodeId, packet);
                }

                Close();
            }

            /// <summary>
            /// Sends the specified packet to this node asynchronously.
            /// The method enqueues a task to write the packet and returns
            /// immediately. This is because SendData() is on a hot path
            /// under the primary lock (BuildManager's _syncLock)
            /// and we want to minimize our time there.
            /// </summary>
            /// <param name="packet">The packet to send.</param>
            public void SendData(INodePacket packet)
            {
                if (IsExitPacket(packet))
                {
                    _exitPacketState = ExitPacketState.ExitPacketQueued;
                }
                _packetWriteQueue.Enqueue(packet);
                DrainPacketQueue();
            }

            /// <summary>
            /// Schedule a task to drain the packet write queue. We could have had a
            /// dedicated thread that would pump the queue constantly, but
            /// we don't want to allocate a dedicated thread per node (1MB stack)
            /// </summary>
            /// <remarks>Usually there'll be a single packet in the queue, but sometimes
            /// a burst of SendData comes in, with 10-20 packets scheduled. In this case
            /// the first scheduled task will drain all of them, and subsequent tasks
            /// will run on an empty queue. I tried to write logic that avoids queueing
            /// a new task if the queue is already being drained, but it didn't show any
            /// improvement and made things more complicated.</remarks>
            private void DrainPacketQueue()
            {
                // this lock is only necessary to protect a write to _packetWriteDrainTask field
                lock (_packetWriteQueue)
                {
                    // average latency between the moment this runs and when the delegate starts
                    // running is about 100-200 microseconds (unless there's thread pool saturation)
                    _packetWriteDrainTask = _packetWriteDrainTask.ContinueWith(
                        SendDataCoreAsync,
                        this,
                        TaskScheduler.Default).Unwrap();

                    static async Task SendDataCoreAsync(Task _, object state)
                    {
                        NodeContext context = (NodeContext)state;
                        while (context._packetWriteQueue.TryDequeue(out INodePacket packet))
                        {
                            try
                            {
                                await context._pipeClient.WritePacketAsync(packet).ConfigureAwait(false);

                                if (IsExitPacket(packet))
                                {
                                    context._exitPacketState = ExitPacketState.ExitPacketSent;
                                }
                            }
                            catch (IOException e)
                            {
                                // Do nothing here because any exception will be caught by the async read handler
                                CommunicationsUtilities.Trace(context._nodeId, "EXCEPTION in SendData: {0}", e);
                            }
                            catch (ObjectDisposedException) // This happens if a child dies unexpectedly
                            {
                                // Do nothing here because any exception will be caught by the async read handler
                            }
                        }
                    }
                }
            }

            private static bool IsExitPacket(INodePacket packet)
            {
                return packet is NodeBuildComplete buildCompletePacket && !buildCompletePacket.PrepareForReuse;
            }

            /// <summary>
            /// Closes the node's context, disconnecting it from the node.
            /// </summary>
            private void Close()
            {
                _pipeClient.Dispose();
                _terminateDelegate(_nodeId);
            }

            /// <summary>
            /// Waits for the child node process to exit.
            /// </summary>
            public async Task WaitForExitAsync(ILoggingService loggingService)
            {
                if (_exitPacketState == ExitPacketState.ExitPacketQueued)
                {
                    // Wait up to 100ms until all remaining packets are sent.
                    // We don't need to wait long, just long enough for the Task to start running on the ThreadPool.
#if NET
                    await _packetWriteDrainTask.WaitAsync(TimeSpan.FromMilliseconds(100)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
#else
                    using (var cts = new CancellationTokenSource(100))
                    {
                        await Task.WhenAny(_packetWriteDrainTask, Task.Delay(100, cts.Token));
                        cts.Cancel();
                    }
#endif
                }

                if (_exitPacketState == ExitPacketState.ExitPacketSent)
                {
                    CommunicationsUtilities.Trace("Waiting for node with pid = {0} to exit", _process.Id);

                    // .NET 5 introduces a real WaitForExitAsyc.
                    // This is a poor man's implementation that uses polling.
                    int timeout = TimeoutForWaitForExit;
                    int delay = 5;
                    while (timeout > 0)
                    {
                        bool exited = _process.WaitForExit(milliseconds: 0);
                        if (exited)
                        {
                            return;
                        }
                        timeout -= delay;
                        await Task.Delay(delay).ConfigureAwait(false);

                        // Double delay up to 500ms.
                        delay = Math.Min(delay * 2, 500);
                    }
                }

                // Kill the child and do a blocking wait.
                loggingService?.LogWarning(
                    BuildEventContext.Invalid,
                    null,
                    BuildEventFileInfo.Empty,
                    "KillingProcessWithPid",
                    _process.Id);
                CommunicationsUtilities.Trace("Killing node with pid = {0}", _process.Id);

                _process.KillTree(timeoutMilliseconds: 5000);
            }
        }
    }
}
