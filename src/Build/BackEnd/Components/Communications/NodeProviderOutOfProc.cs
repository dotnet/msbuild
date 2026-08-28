// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The provider for out-of-proc nodes.  This manages the lifetime of external MSBuild.exe processes
    /// which act as child nodes for the build system.
    /// </summary>
    internal class NodeProviderOutOfProc : NodeProviderOutOfProcBase, INodeProvider
    {
        /// <summary>
        /// A mapping of all the nodes managed by this provider.
        /// </summary>
        private ConcurrentDictionary<int, NodeContext> _nodeContexts;

        private readonly object _multiNodeProcessLock = new();

        private readonly ManualResetEventSlim _multiNodeProcessShutdownComplete = new(initialState: true);

        private Queue<MultiNodeProcessSlot> _availableMultiNodeProcessSlots;

        private Process _multiNodeProcess;

        private bool _multiNodeProcessShuttingDown;

        private HandshakeOptions _multiNodeProcessHandshakeOptions;

        private sealed class MultiNodeProcessSlot
        {
            internal MultiNodeProcessSlot(Stream stream, byte negotiatedPacketVersion)
            {
                Stream = stream;
                NegotiatedPacketVersion = negotiatedPacketVersion;
            }

            internal Stream Stream { get; }

            internal byte NegotiatedPacketVersion { get; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        private NodeProviderOutOfProc()
        {
        }

        #region INodeProvider Members

        /// <summary>
        /// Returns the node provider type.
        /// </summary>
        public NodeProviderType ProviderType
        {
            [DebuggerStepThrough]
            get
            { return NodeProviderType.OutOfProc; }
        }

        /// <summary>
        /// Returns the number of available nodes.
        /// </summary>
        public int AvailableNodes
        {
            get
            {
                return ComponentHost.BuildParameters.MaxNodeCount - _nodeContexts.Count;
            }
        }

        /// <summary>
        /// Magic number sent by the host to the client during the handshake.
        /// Derived from the binary timestamp to avoid mixing binary versions,
        /// Is64BitProcess to avoid mixing bitness, and enableNodeReuse to
        /// ensure that a /nr:false build doesn't reuse clients left over from
        /// a prior /nr:true build. The enableLowPriority flag is to ensure that
        /// a build with /low:false doesn't reuse clients left over for a prior
        /// /low:true build.
        /// </summary>
        /// <param name="enableNodeReuse">Is reuse of build nodes allowed?</param>
        /// <param name="enableLowPriority">Is the build running at low priority?</param>
        internal static Handshake GetHandshake(bool enableNodeReuse, bool enableLowPriority)
        {
            CommunicationsUtilities.Trace($"""MSBUILDNODEHANDSHAKESALT="{Traits.MSBuildNodeHandshakeSalt}", msbuildDirectory="{BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32}", enableNodeReuse={enableNodeReuse}, enableLowPriority={enableLowPriority}""");
            return new Handshake(CommunicationsUtilities.GetHandshakeOptions(taskHost: false, taskHostParameters: TaskHostParameters.Empty, architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture(), nodeReuse: enableNodeReuse, lowPriority: enableLowPriority));
        }

        /// <summary>
        /// Instantiates a new MSBuild processes acting as a child nodes or connect to existing ones.
        /// </summary>
        public IList<NodeInfo> CreateNodes(int nextNodeId, INodePacketFactory factory, Func<NodeInfo, NodeConfiguration> configurationFactory, int numberOfNodesToCreate)
        {
            ArgumentNullException.ThrowIfNull(factory);

            if (ShouldUseSingleProcessForMultiThreadedNodes(ComponentHost.BuildParameters))
            {
                return CreateNodesInSingleProcess(nextNodeId, factory, configurationFactory, numberOfNodesToCreate);
            }

            // This can run concurrently. To be properly detect internal bug when we create more nodes than allowed
            //   we add into _nodeContexts premise of future node and verify that it will not cross limits.
            if (_nodeContexts.Count + numberOfNodesToCreate > ComponentHost.BuildParameters.MaxNodeCount)
            {
                return InternalError.Throw<IList<NodeInfo>>($"Exceeded max node count of '{ComponentHost.BuildParameters.MaxNodeCount}', current count is '{_nodeContexts.Count}' ");
            }

            ConcurrentBag<NodeInfo> nodes = new();
            Handshake hostHandshake = new(CommunicationsUtilities.GetHandshakeOptions(taskHost: false, taskHostParameters: TaskHostParameters.Empty, architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture(), nodeReuse: ComponentHost.BuildParameters.EnableNodeReuse, lowPriority: ComponentHost.BuildParameters.LowPriority));

            // Start the new process.  We pass in a node mode with a node number of 1, to indicate that we
            // want to start up just a standard MSBuild out-of-proc node.
            // Note: We need to always pass /nodeReuse to ensure the value for /nodeReuse from msbuild.rsp
            // (next to msbuild.exe) is ignored.
            NodeLaunchData nodeLaunchData = new(
                MSBuildLocation: null,
                CommandLineArgs: $"/noautoresponse /nologo {NodeModeHelper.ToCommandLineArgument(NodeMode.OutOfProcNode)} /nodeReuse:{ComponentHost.BuildParameters.EnableNodeReuse.ToString().ToLower()} /low:{ComponentHost.BuildParameters.LowPriority.ToString().ToLower()}",
                Handshake: hostHandshake,
                EnvironmentOverrides: DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides());

            CommunicationsUtilities.Trace($"Starting to acquire {numberOfNodesToCreate} new or existing node(s) to establish nodes from ID {nextNodeId} to {nextNodeId + numberOfNodesToCreate - 1}...");
     
            IList<NodeContext> nodeContexts = GetNodes(nodeLaunchData, nextNodeId, factory, NodeContextCreated, NodeContextTerminated, numberOfNodesToCreate);

            if (nodeContexts.Count > 0)
            {
                return nodeContexts
                    .Select(nc => new NodeInfo(nc.NodeId, ProviderType))
                    .ToList();
            }

            throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotConnectToMSBuildExe", ComponentHost.BuildParameters.NodeExeLocation));

            void NodeContextCreated(NodeContext context)
            {
                NodeInfo nodeInfo = new NodeInfo(context.NodeId, ProviderType);

                _nodeContexts[context.NodeId] = context;

                // Start the asynchronous read.
                context.BeginAsyncPacketRead();

                // Configure the node.
                context.SendData(configurationFactory(nodeInfo));
            }
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="nodeId">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(int nodeId, INodePacket packet)
        {
            Assumed.True(_nodeContexts.ContainsKey(nodeId), $"Invalid node id specified: {nodeId}.");

            SendData(_nodeContexts[nodeId], packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            if (ShutdownMultiNodeProcess())
            {
                return;
            }

            // Send the build completion message to the nodes, causing them to shutdown or reset.
            var contextsToShutDown = new List<NodeContext>(_nodeContexts.Values);

            ShutdownConnectedNodes(contextsToShutDown, enableReuse);
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
            if (ShutdownMultiNodeProcess())
            {
                return;
            }

            // If no BuildParameters were specified for this build,
            // we must be trying to shut down idle nodes from some
            // other, completed build. If they're still around,
            // they must have been started with node reuse.
            bool nodeReuse = ComponentHost.BuildParameters?.EnableNodeReuse ?? true;

            // To avoid issues with mismatched priorities not shutting
            // down all the nodes on exit, we will attempt to shutdown
            // all matching nodes with and without the priority bit set.
            // This means we need both versions of the handshake.
            ShutdownAllNodes(nodeReuse, NodeContextTerminated);
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Initializes the component.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            this.ComponentHost = host;
            _nodeContexts = new ConcurrentDictionary<int, NodeContext>();
            _availableMultiNodeProcessSlots = new Queue<MultiNodeProcessSlot>();
        }

        /// <summary>
        /// Shuts down the component
        /// </summary>
        public void ShutdownComponent()
        {
            ShutdownMultiNodeProcess();
        }

        #endregion

        /// <summary>
        /// Static factory for component creation.
        /// </summary>
        internal static IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            Assumed.Equal(componentType, BuildComponentType.OutOfProcNodeProvider, $"Factory cannot create components of type {componentType}");
            return new NodeProviderOutOfProc();
        }

        /// <summary>
        /// Method called when a context terminates.
        /// </summary>
        private void NodeContextTerminated(int nodeId)
        {
            _nodeContexts.TryRemove(nodeId, out _);
        }

        public IEnumerable<Process> GetProcesses()
        {
            Process multiNodeProcess;
            lock (_multiNodeProcessLock)
            {
                multiNodeProcess = _multiNodeProcess;
            }

            if (multiNodeProcess != null)
            {
                return [multiNodeProcess];
            }

            return _nodeContexts.Values
                .Select(context => context.Process)
                .GroupBy(process => process.Id)
                .Select(group => group.First());
        }

        internal static bool ShouldUseSingleProcessForMultiThreadedNodes(BuildParameters parameters)
            => parameters.MultiThreaded && parameters.DisableInProcNode;

        private IList<NodeInfo> CreateNodesInSingleProcess(
            int nextNodeId,
            INodePacketFactory factory,
            Func<NodeInfo, NodeConfiguration> configurationFactory,
            int numberOfNodesToCreate)
        {
            lock (_multiNodeProcessLock)
            {
                if (_multiNodeProcessShuttingDown)
                {
                    throw new BuildAbortedException("The multi-node worker is shutting down.");
                }

                if (_nodeContexts.Count + numberOfNodesToCreate > ComponentHost.BuildParameters.MaxNodeCount)
                {
                    return InternalError.Throw<IList<NodeInfo>>(
                        $"Exceeded max node count of '{ComponentHost.BuildParameters.MaxNodeCount}', current count is '{_nodeContexts.Count}'.");
                }

                EnsureMultiNodeProcess(nextNodeId);

                if (_availableMultiNodeProcessSlots.Count < numberOfNodesToCreate)
                {
                    throw new BuildAbortedException(
                        $"The multi-node worker has {_availableMultiNodeProcessSlots.Count} available logical slots, but {numberOfNodesToCreate} were requested.");
                }

                try
                {
                    var nodes = new List<NodeInfo>(numberOfNodesToCreate);
                    for (int i = 0; i < numberOfNodesToCreate; i++)
                    {
                        int nodeId = nextNodeId + i;
                        NodeInfo nodeInfo = new(nodeId, ProviderType);
                        MultiNodeProcessSlot slot = _availableMultiNodeProcessSlots.Dequeue();
                        NodeContext context = new(
                            nodeId,
                            _multiNodeProcess,
                            slot.Stream,
                            factory,
                            NodeContextTerminated,
                            slot.NegotiatedPacketVersion,
                            _multiNodeProcessHandshakeOptions);

                        _nodeContexts[nodeId] = context;
                        context.BeginAsyncPacketRead();
                        context.SendData(configurationFactory(nodeInfo));
                        nodes.Add(nodeInfo);

                        CommunicationsUtilities.Trace(
                            $"Assigned logical node {nodeId} to multi-node worker PID {_multiNodeProcess.Id}.");
                    }

                    return nodes;
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    Process process = _multiNodeProcess;
                    CommunicationsUtilities.Trace(
                        $"Failed while assigning logical nodes in multi-node worker PID {process.Id}: {ex}.");
                    TerminateMultiNodeProcess(process);
                    _nodeContexts.Clear();
                    _multiNodeProcess = null;
                    _availableMultiNodeProcessSlots.Clear();
                    throw new BuildAbortedException(
                        $"Could not configure logical nodes in multi-node worker PID {process.Id}.",
                        ex);
                }
            }
        }

        private void EnsureMultiNodeProcess(int firstNodeId)
        {
            if (_multiNodeProcess != null && !_multiNodeProcess.HasExited)
            {
                return;
            }

            _availableMultiNodeProcessSlots.Clear();
            int slotCount = ComponentHost.BuildParameters.MaxNodeCount;
            Handshake handshake = new(CommunicationsUtilities.GetHandshakeOptions(
                taskHost: false,
                taskHostParameters: TaskHostParameters.Empty,
                architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture(),
                nodeReuse: false,
                lowPriority: ComponentHost.BuildParameters.LowPriority));
            _multiNodeProcessHandshakeOptions = handshake.HandshakeOptions;

            string msbuildLocation = ComponentHost.BuildParameters.NodeExeLocation;
#if RUNTIME_TYPE_NETCORE
            msbuildLocation = RemapAppHostToManagedDllIfHostedByDotnet(msbuildLocation);
#endif
            NodeLaunchData launchData = new(
                MSBuildLocation: msbuildLocation,
                CommandLineArgs: $"/noautoresponse /nologo {NodeModeHelper.ToCommandLineArgument(NodeMode.OutOfProcMultiNode)} /nodeReuse:false /low:{ComponentHost.BuildParameters.LowPriority.ToString().ToLower()} /m:{slotCount}",
                Handshake: handshake,
                EnvironmentOverrides: CreateMultiNodeWorkerEnvironmentOverrides());

            INodeLauncher nodeLauncher = (INodeLauncher)ComponentHost.GetComponent(BuildComponentType.NodeLauncher);
            Process process = nodeLauncher.Start(launchData, firstNodeId);
            var slots = new MultiNodeProcessSlot[slotCount];
            ConcurrentQueue<Exception> exceptions = new();

            CommunicationsUtilities.Trace(
                $"Started multi-node worker PID {process.Id} with {slotCount} logical slot(s).");

            Parallel.For(0, slotCount, slot =>
            {
                try
                {
                    string pipeName = NamedPipeUtil.GetPlatformSpecificPipeName(process.Id, slot);
                    Stream stream = TryConnectToProcess(
                        process.Id,
                        TimeoutForNewNodeCreation,
                        handshake,
                        out HandshakeResult result,
                        pipeName);

                    if (stream is null)
                    {
                        throw new IOException($"Could not connect to logical slot {slot} on worker PID {process.Id}.");
                    }

                    slots[slot] = new MultiNodeProcessSlot(stream, result.NegotiatedPacketVersion);
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                }
            });

            if (!exceptions.IsEmpty || slots.Any(slot => slot is null))
            {
                foreach (MultiNodeProcessSlot slot in slots)
                {
                    slot?.Stream.Dispose();
                }

                TerminateMultiNodeProcess(process);
                throw new BuildAbortedException(
                    $"Could not initialize all {slotCount} logical slots in multi-node worker PID {process.Id}.",
                    new AggregateException(exceptions));
            }

            _multiNodeProcess = process;
            foreach (MultiNodeProcessSlot slot in slots)
            {
                _availableMultiNodeProcessSlots.Enqueue(slot);
            }
        }

        internal static IDictionary<string, string> CreateMultiNodeWorkerEnvironmentOverrides()
        {
            IDictionary<string, string> baseOverrides = DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides();
            var environmentOverrides = baseOverrides is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(baseOverrides);

            if (Environment.GetEnvironmentVariable("DOTNET_gcServer") is null)
            {
                environmentOverrides["DOTNET_gcServer"] = "1";
            }

            return environmentOverrides;
        }

        private bool ShutdownMultiNodeProcess()
        {
            Process process;
            List<NodeContext> contexts;
            bool waitForInProgressShutdown;
            lock (_multiNodeProcessLock)
            {
                process = _multiNodeProcess;
                if (process is null)
                {
                    return false;
                }

                if (_multiNodeProcessShuttingDown)
                {
                    waitForInProgressShutdown = true;
                    contexts = null;
                }
                else
                {
                    waitForInProgressShutdown = false;
                    _multiNodeProcessShuttingDown = true;
                    _multiNodeProcessShutdownComplete.Reset();

                    while (_availableMultiNodeProcessSlots.Count > 0)
                    {
                        MultiNodeProcessSlot slot = _availableMultiNodeProcessSlots.Dequeue();
                        slot.Stream.Dispose();
                    }

                    contexts = new List<NodeContext>(_nodeContexts.Values);
                }
            }

            if (waitForInProgressShutdown)
            {
                _multiNodeProcessShutdownComplete.Wait();
                return true;
            }

            bool shutdownPacketsSent = true;
            foreach (NodeContext context in contexts)
            {
                try
                {
                    SendData(context, new NodeBuildComplete(prepareForReuse: false));
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    shutdownPacketsSent = false;
                    CommunicationsUtilities.Trace(
                        $"Failed to send a shutdown packet to multi-node worker PID {process.Id}: {ex}.");
                }
            }

            if (!shutdownPacketsSent)
            {
                CommunicationsUtilities.Trace(
                    $"Not all logical slots in multi-node worker PID {process.Id} accepted shutdown; waiting before forced termination.");
            }

            CompleteMultiNodeProcessShutdown(process);
            return true;
        }

        private void CompleteMultiNodeProcessShutdown(Process process)
        {
            try
            {
                if (!WaitForMultiNodeProcessExit(
                    process.WaitForExit,
                    () => TerminateMultiNodeProcess(process),
                    TimeoutForWaitForExit))
                {
                    CommunicationsUtilities.Trace(
                        $"Multi-node worker PID {process.Id} did not exit cleanly and was terminated.");
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                CommunicationsUtilities.Trace($"Failed while shutting down multi-node worker PID {process.Id}: {ex}.");
                TerminateMultiNodeProcess(process);
            }
            finally
            {
                lock (_multiNodeProcessLock)
                {
                    if (ReferenceEquals(_multiNodeProcess, process))
                    {
                        _nodeContexts.Clear();
                        _multiNodeProcess = null;
                        _multiNodeProcessShuttingDown = false;
                        _availableMultiNodeProcessSlots.Clear();
                    }
                }

                _multiNodeProcessShutdownComplete.Set();
            }
        }

        internal static bool WaitForMultiNodeProcessExit(
            Func<int, bool> waitForExit,
            Action terminate,
            int timeoutMilliseconds)
        {
            if (waitForExit(timeoutMilliseconds))
            {
                return true;
            }

            terminate();
            return false;
        }

        private static void TerminateMultiNodeProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.KillTree(timeoutMilliseconds: 5000);
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                CommunicationsUtilities.Trace($"Failed to terminate multi-node worker PID {process.Id}: {ex}.");
            }
        }
    }
}
