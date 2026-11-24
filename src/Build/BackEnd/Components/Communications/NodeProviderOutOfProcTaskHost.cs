// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if RUNTIME_TYPE_NETCORE
using System.Text;
#endif
using System.Threading;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Constants = Microsoft.Build.Framework.Constants;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    internal readonly record struct TaskHostLaunchIdentity(
        string ExecutablePath,
        string CommandLineArgs,
        string DotnetHostPath);

    /// <summary>
    /// Represents a unique key for identifying task host nodes.
    /// Combines the typed task-host request, scheduled node ID, console forwarding, and resolved launch identity.
    /// The scheduled node ID is -1 in traditional multi-process builds.
    /// </summary>
    internal readonly record struct TaskHostNodeKey
    {
        internal TaskHostNodeKey(TaskHostRequest request, int nodeId, bool forwardConsoleOutput = false, TaskHostLaunchIdentity? launchIdentity = null)
        {
            Request = WithoutLaunchPaths(request);
            NodeId = nodeId;
            ForwardConsoleOutput = forwardConsoleOutput;
            LaunchIdentity = launchIdentity;
        }

        /// <summary>
        /// Returns a copy of <paramref name="request"/> that keeps only its kind and wire options.
        /// </summary>
        /// <remarks>
        /// Requested launch paths are inputs to path resolution, not part of node identity.
        /// The resolved executable is tracked by <see cref="LaunchIdentity"/>, so retaining the
        /// requested paths would make otherwise-equal keys differ and prevent task-host reuse.
        /// </remarks>
        private static TaskHostRequest WithoutLaunchPaths(TaskHostRequest request)
            => request switch
            {
                NetTaskHostRequest netRequest => new NetTaskHostRequest(netRequest.Options),
                FrameworkTaskHostRequest frameworkRequest => new FrameworkTaskHostRequest(frameworkRequest.Options),
                Clr2TaskHostRequest clr2Request => new Clr2TaskHostRequest(clr2Request.Options),
            };

        internal TaskHostRequest Request { get; }
        internal int NodeId { get; }
        internal bool ForwardConsoleOutput { get; }
        internal TaskHostLaunchIdentity? LaunchIdentity { get; init; }
    }

    /// <summary>
    /// The provider for out-of-proc nodes.  This manages the lifetime of external MSBuild.exe processes
    /// which act as child nodes for the build system.
    /// </summary>
    internal class NodeProviderOutOfProcTaskHost : NodeProviderOutOfProcBase<TaskHostHandshake>, INodeProvider, INodePacketFactory, INodePacketHandler
    {
        /// <summary>
        /// The provider used by a worker node, whose lifetime is the process rather than the build.
        /// </summary>
        private static NodeProviderOutOfProcTaskHost s_processWideInstance;
        private readonly bool _processWide;

        /// <summary>
        /// Store the path for MSBuild / MSBuildTaskHost so that we don't have to keep recalculating it.
        /// </summary>
        private static string s_baseTaskHostPath;

        /// <summary>
        /// Store the 64-bit path for MSBuild / MSBuildTaskHost so that we don't have to keep recalculating it.
        /// </summary>
        private static string s_baseTaskHostPath64;

        /// <summary>
        /// Store the 64-bit path for MSBuild / MSBuildTaskHost so that we don't have to keep recalculating it.
        /// </summary>
        private static string s_baseTaskHostPathArm64;

        /// <summary>
        /// Store the path for the 32-bit MSBuildTaskHost so that we don't have to keep re-calculating it.
        /// </summary>
        private static string s_pathToX32Clr2;

        /// <summary>
        /// Store the path for the 64-bit MSBuildTaskHost so that we don't have to keep re-calculating it.
        /// </summary>
        private static string s_pathToX64Clr2;

        /// <summary>
        /// Store the path for the 32-bit MSBuild so that we don't have to keep re-calculating it.
        /// </summary>
        private static string s_pathToX32Clr4;

        /// <summary>
        /// Store the path for the 64-bit MSBuild so that we don't have to keep re-calculating it.
        /// </summary>
        private static string s_pathToX64Clr4;

        /// <summary>
        /// Store the path for the 64-bit MSBuild so that we don't have to keep re-calculating it.
        /// </summary>
        private static string s_pathToArm64Clr4;

        /// <summary>
        /// Name for MSBuild.exe
        /// </summary>
        private static string s_msbuildName;

        /// <summary>
        /// Name for MSBuildTaskHost.exe
        /// </summary>
        private static string s_msbuildTaskHostName;

        /// <summary>
        /// Are there any active nodes?
        /// </summary>
        private ManualResetEvent _noNodesActiveEvent;

        /// <summary>
        /// A mapping of all the task host nodes managed by this provider.
        /// The key is a TaskHostNodeKey combining the typed task-host request and scheduled node ID.
        /// </summary>
        private ConcurrentDictionary<TaskHostNodeKey, NodeContext<TaskHostHandshake>> _nodeContexts;

        /// <summary>
        /// Reverse mapping from communication node ID to TaskHostNodeKey.
        /// Used for O(1) lookup when handling node termination from ShutdownAllNodes.
        /// </summary>
        private ConcurrentDictionary<int, TaskHostNodeKey> _nodeIdToNodeKey;

        /// <summary>
        /// Per-node handler stacks for routing packets from OOP TaskHost processes.
        /// Keyed by communication node ID (one per OOP process). Each node can have
        /// multiple OOP processes for different architectures (x86, x64, ARM64).
        /// The stack supports nested BuildProjectFile callbacks: when Task A calls
        /// BuildProjectFile and blocks, Task B is dispatched to the same process --
        /// handler B is pushed on top. Packets route to Peek() (the active task).
        /// A callback that reacquires the node moves its handler back to the top,
        /// since resumption need not follow dispatch order.
        /// </summary>
        private ConcurrentDictionary<int, Stack<INodePacketHandler>> _nodeIdToPacketHandlerStack;

        /// <summary>
        /// Communication node IDs explicitly enabled for console forwarding.
        /// </summary>
        private HashSet<int> _consoleForwardingNodeIds;

        private readonly LockType _consoleForwardingLock = new();

        private bool _consoleOutputForwarded;

        private bool _isShutDown;

        /// <summary>
        /// Keeps track of the set of node IDs for which we have not yet received shutdown notification.
        /// </summary>
        private HashSet<int> _activeNodes;

        /// <summary>
        /// Counter for generating unique communication node IDs.
        /// Incremented atomically for each new node created.
        /// </summary>
        private int _nextNodeId;

        /// <summary>
        /// Packet factory we use if there's not already one associated with a particular context.
        /// </summary>
        private NodePacketFactory _localPacketFactory;

        /// <summary>
        /// Constructor.
        /// </summary>
        private NodeProviderOutOfProcTaskHost(bool processWide = false)
        {
            _processWide = processWide;
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
                throw new NotImplementedException("This property is not implemented because available nodes are unlimited.");
            }
        }

        internal bool ConsoleOutputForwarded
        {
            get
            {
                lock (_consoleForwardingLock)
                {
                    return _consoleOutputForwarded;
                }
            }
        }

        /// <summary>
        /// Returns the name of the CLR2 Task Host executable
        /// </summary>
        internal static string TaskHostNameForClr2TaskHost
        {
            get
            {
                string name = s_msbuildTaskHostName;
                if (name is null)
                {
                    name = Environment.GetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME") ?? "MSBuildTaskHost.exe";
                    s_msbuildTaskHostName = name;
                }

                return name;
            }
        }

        /// <summary>
        /// Instantiates a new MSBuild process acting as a child node.
        /// </summary>
        public IList<NodeInfo> CreateNodes(int nextNodeId, INodePacketFactory packetFactory, Func<NodeInfo, NodeConfiguration> configurationFactory, int numberOfNodesToCreate)
        {
            throw new NotImplementedException("Use the other overload of CreateNode instead");
        }

        /// <summary>
        /// Sends data to the specified node.
        /// Task hosts send through the connection returned by AcquireAndSetUpHost.
        /// </summary>
        /// <param name="nodeId">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(int nodeId, INodePacket packet)
        {
            throw new NotImplementedException("Task hosts send through their acquired connection.");
        }

        /// <summary>
        /// Whether a task host launched with this handshake is owned by this process, staying
        /// connected to it between builds instead of disconnecting into the pool of task hosts any
        /// process may claim.
        /// </summary>
        /// <remarks>
        /// Explicit <c>TaskFactory="TaskHostFactory"</c> requests disable node reuse before this
        /// check. Among reusable task hosts, only sidecars stay connected. Hosts needed for a
        /// different runtime or architecture remain pooled so other processes can use them.
        ///
        /// Behind <see cref="ChangeWaves.Wave18_12"/>: opting out pools every reusable task host.
        /// Both endpoints must support cleanup acknowledgments. A legacy reuse flag alone does
        /// not establish ownership.
        /// </remarks>
        protected override bool DoesConnectionPersistAcrossBuilds(TaskHostHandshake handshake, byte negotiatedVersion)
            => SupportsSidecarLifetime(ExistsOnlyForCompatibility(handshake), negotiatedVersion)
                && handshake.NodeReuse;

        private static bool SupportsSidecarLifetime(TaskHostRequest request, byte negotiatedVersion)
            => SupportsSidecarLifetime(ExistsOnlyForCompatibility(request), negotiatedVersion);

        private static bool SupportsSidecarLifetime(bool existsOnlyForCompatibility, byte negotiatedVersion)
            => ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_12)
                && negotiatedVersion >= NodePacketTypeExtensions.TaskHostOwnershipMinVersion
                && !existsOnlyForCompatibility;

        /// <summary>
        /// Determines whether the connected task host exists only because this process cannot run
        /// the task itself, rather than to keep a task out of this process.
        /// </summary>
        /// <remarks>
        /// Uses the shared compatibility policy also applied to requests before launching a host,
        /// so pooling and launch decisions remain consistent.
        /// </remarks>
        private static bool ExistsOnlyForCompatibility(TaskHostHandshake handshake)
            => handshake switch
            {
                NetTaskHostHandshake => NetTaskHostExistsOnlyForCompatibility,
                FrameworkTaskHostHandshake frameworkHandshake => ArchitectureDiffersFromCurrentProcess(frameworkHandshake.Options.Architecture),
                Clr2TaskHostHandshake => true,
            };

        /// <summary>
        /// Determines whether the requested task host is needed only for runtime or architecture
        /// compatibility with this process.
        /// </summary>
        /// <remarks>
        /// The request and handshake are separate typed unions. Both overloads apply the same
        /// per-runtime rules, so pooling and launch decisions remain consistent.
        /// </remarks>
        private static bool ExistsOnlyForCompatibility(TaskHostRequest request)
            => request switch
            {
                NetTaskHostRequest => NetTaskHostExistsOnlyForCompatibility,
                FrameworkTaskHostRequest frameworkRequest => ArchitectureDiffersFromCurrentProcess(frameworkRequest.Options.Architecture),
                Clr2TaskHostRequest => true,
            };

        /// <summary>
        /// A .NET task host runs a different runtime only when this process is not itself .NET.
        /// </summary>
        /// <remarks>
        /// In a .NET process the runtime is the same. A .NET task host runs whatever architecture
        /// the SDK shipped and suppresses its architecture bits for that reason, so they carry no
        /// information here and there is nothing further to compare.
        /// </remarks>
        private const bool NetTaskHostExistsOnlyForCompatibility =
#if NETFRAMEWORK
            true;
#else
            false;
#endif

        /// <summary>
        /// Determines whether a .NET Framework task host runs a different architecture than this process.
        /// </summary>
        private static bool ArchitectureDiffersFromCurrentProcess(TaskHostArchitecture architecture)
        {
            // The handshake encodes only x64 and arm64, so no bit means x86 -- which differs from
            // this process unless this process is itself x86.
            string requestedArchitecture = architecture switch
            {
                TaskHostArchitecture.X64 => XMakeAttributes.MSBuildArchitectureValues.x64,
                TaskHostArchitecture.Arm64 => XMakeAttributes.MSBuildArchitectureValues.arm64,
                _ => XMakeAttributes.MSBuildArchitectureValues.x86,
            };

            return !string.Equals(requestedArchitecture, XMakeAttributes.GetCurrentMSBuildArchitecture(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
#if RUNTIME_TYPE_NETCORE
            // Query Visual Studio installations again for the next build, so that installing or removing one takes effect
            // in a process that serves several builds.
            s_visualStudioInstances = null;
#endif

            if (_nodeContexts is null)
            {
                return;
            }

            List<NodeContext<TaskHostHandshake>> contextsToShutDown = [.. _nodeContexts.Values];

            // Processes that could not be connected to are skipped for the rest of a build; clear
            // that for the next one, or a pooled task host this process failed to claim once is
            // never retried and a new one is started instead. The worker node provider is shared
            // across every build the process serves, so this would otherwise accumulate.
            ClearProcessesToIgnore();

            bool waitForCleanup = false;

            // Retire terminal sidecars before sending shutdown, so a new build cannot acquire them.
            lock (_activeNodes)
            {
                foreach (NodeContext<TaskHostHandshake> context in contextsToShutDown)
                {
                    if (!_nodeIdToNodeKey.TryGetValue(context.NodeId, out TaskHostNodeKey nodeKey))
                    {
                        continue;
                    }

                    if (!enableReuse && SupportsSidecarLifetime(nodeKey.Request, context.NegotiatedPacketVersion))
                    {
                        RetireNode(context.NodeId);
                    }
                    else
                    {
                        _activeNodes.Add(context.NodeId);
                        waitForCleanup = true;
                    }
                }

                if (_activeNodes.Count == 0)
                {
                    _noNodesActiveEvent.Set();
                }
                else
                {
                    _noNodesActiveEvent.Reset();
                }
            }

            foreach (NodeContext<TaskHostHandshake> context in contextsToShutDown)
            {
                NodeBuildCompleteAction action = !enableReuse
                    ? NodeBuildCompleteAction.Shutdown
                    : context.ConnectionPersistsAcrossBuilds
                        ? NodeBuildCompleteAction.ReuseWithConnection
                        : NodeBuildCompleteAction.Legacy;
                context.SendData(new NodeBuildComplete(enableReuse, action));
            }

            if (waitForCleanup)
            {
                _noNodesActiveEvent.WaitOne();
            }
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
            // Sidecars stay connected between builds, so they are reachable here and wouldn't be found
            // by the scan below, which looks for nodes this process is not connected to.
            ShutdownConnectedNodes(enableReuse: false);

            ShutdownAllNodes(ComponentHost.BuildParameters.EnableNodeReuse, NodeContextTerminated);
        }
        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// The connections this provider owns, or <see langword="null"/> before initialization.
        /// FOR UNIT TESTING ONLY: a worker node process must keep the same set across every build it
        /// serves, or task hosts that are still connected are lost.
        /// </summary>
        internal ConcurrentDictionary<TaskHostNodeKey, NodeContext<TaskHostHandshake>> ConnectedNodes => _nodeContexts;

        /// <summary>
        /// The host of the build currently being served. FOR UNIT TESTING ONLY.
        /// </summary>
        internal IBuildComponentHost CurrentComponentHost => ComponentHost;

        /// <summary>
        /// Whether a node launched with this handshake keeps its connection between builds.
        /// FOR UNIT TESTING ONLY.
        /// </summary>
        internal bool ConnectionPersists(TaskHostHandshake handshake, byte negotiatedVersion = NodePacketTypeExtensions.PacketVersion)
            => DoesConnectionPersistAcrossBuilds(handshake, negotiatedVersion);

        /// <summary>
        /// Initializes the component.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            ComponentHost = host;

            // This provider outlives the component collection that resolved it. A worker node
            // process serves many builds and constructs a fresh OutOfProcNode for each one (see
            // MSBuildApp.StartLocalNode), while the task hosts it launched stay connected to this
            // process across those builds. Re-initializing here would strand those connections:
            // unreachable from this process because nothing references them, and unclaimable by any
            // other because a task host pipe accepts only one connection at a time. So once the
            // node state exists, only refresh the host.
            if (_nodeContexts is not null)
            {
                return;
            }

            _nodeContexts = new ConcurrentDictionary<TaskHostNodeKey, NodeContext<TaskHostHandshake>>();
            _nodeIdToNodeKey = new ConcurrentDictionary<int, TaskHostNodeKey>();
            _nodeIdToPacketHandlerStack = new ConcurrentDictionary<int, Stack<INodePacketHandler>>();
            _consoleForwardingNodeIds = [];
            _consoleOutputForwarded = false;
            _activeNodes = [];
            _nextNodeId = 0;
            _isShutDown = false;

            _noNodesActiveEvent = new ManualResetEvent(true);
            _localPacketFactory = new NodePacketFactory();

            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.LogMessage, LogMessagePacket.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.TaskHostTaskComplete, TaskHostTaskComplete.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.NodeBuildComplete, NodeBuildComplete.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.ConsoleWrite, ConsoleWritePacket.FactoryForDeserialization, this);

            // Register callback request packet types so we can deserialize them when
            // they arrive from TaskHost processes. These are forwarded to the current
            // TaskHostTask handler via the handler stack.
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.TaskHostIsRunningMultipleNodesRequest, TaskHostIsRunningMultipleNodesRequest.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.TaskHostCoresRequest, TaskHostCoresRequest.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.TaskHostBuildRequest, TaskHostBuildRequest.FactoryForDeserialization, this);
        }

        /// <summary>
        /// Shuts down the component
        /// </summary>
        public void ShutdownComponent()
        {
            lock (_consoleForwardingLock)
            {
                _isShutDown = !_processWide;
                _consoleForwardingNodeIds.Clear();
            }

            if (!_processWide)
            {
                ShutdownConnectedNodes(enableReuse: false);
            }
        }

        /// <summary>
        /// Prevents packets from task hosts belonging to the completed build from reaching a later build's console.
        /// </summary>
        internal void ClearPerBuildState()
        {
            lock (_consoleForwardingLock)
            {
                _consoleForwardingNodeIds.Clear();
                _consoleOutputForwarded = false;
            }
        }

        #endregion

        #region INodePacketFactory Members

        /// <summary>
        /// Registers the specified handler for a particular packet type.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="factory">The factory for packets of the specified type.</param>
        /// <param name="handler">The handler to be called when packets of the specified type are received.</param>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _localPacketFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            _localPacketFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Takes a serializer, deserializes the packet and routes it to the appropriate handler.
        /// Always uses the local packet factory for deserialization, which routes through
        /// our PacketReceived method (using the handler stack).
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            // Always route through our local factory which handles deserialization
            // and routes to our PacketReceived, which uses the handler stack.
            _localPacketFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Takes a serializer and deserializes the packet.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator)
        {
            return _localPacketFactory.DeserializePacket(packetType, translator);
        }

        /// <summary>
        /// Routes the specified packet through our PacketReceived method (handler stack).
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            _localPacketFactory.RoutePacket(nodeId, packet);
        }

        #endregion

        #region INodePacketHandler Members

        /// <summary>
        /// This method is invoked by the NodePacketRouter when a packet is received and is intended for
        /// this recipient.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
        public void PacketReceived(int node, INodePacket packet)
        {
            if (packet is NodeBuildComplete buildComplete)
            {
                Assumed.True(buildComplete.PrepareForReuse);
                Assumed.Equal(buildComplete.Action, NodeBuildCompleteAction.ReuseWithConnection);
                lock (_activeNodes)
                {
                    _activeNodes.Remove(node);
                    if (_activeNodes.Count == 0)
                    {
                        _noNodesActiveEvent.Set();
                    }
                }
                return;
            }

            if (packet.Type == NodePacketType.NodeShutdown)
            {
                INodePacketHandler[] handlers = [];
                lock (_activeNodes)
                {
                    // Prevent a late acquisition from attaching after the terminal notification.
                    if (_nodeIdToNodeKey.TryRemove(node, out TaskHostNodeKey nodeKey))
                    {
                        _nodeContexts.TryRemove(nodeKey, out _);
                    }

                    if (_nodeIdToPacketHandlerStack.TryRemove(node, out Stack<INodePacketHandler> shutdownHandlers))
                    {
                        lock (shutdownHandlers)
                        {
                            handlers = shutdownHandlers.ToArray();
                            shutdownHandlers.Clear();
                        }
                    }
                }

                // Nested and blocked tasks all need the failure, not just the top of the stack.
                foreach (INodePacketHandler handler in handlers)
                {
                    handler.PacketReceived(node, packet);
                }
                return;
            }

            if (packet is ConsoleWritePacket consoleWrite)
            {
                lock (_consoleForwardingLock)
                {
                    if (!_isShutDown && _consoleForwardingNodeIds.Contains(node))
                    {
                        switch (consoleWrite.OutputType)
                        {
                            case ConsoleOutput.Standard:
                                Console.Out.Write(consoleWrite.Text);
                                break;
                            case ConsoleOutput.Error:
                                Console.Error.Write(consoleWrite.Text);
                                break;
                            default:
                                InternalError.Throw($"Unexpected console output type {consoleWrite.OutputType}");
                                break;
                        }

                        _consoleOutputForwarded |= !string.IsNullOrEmpty(consoleWrite.Text);
                    }
                }

                return;
            }

            if (_nodeIdToPacketHandlerStack.TryGetValue(node, out Stack<INodePacketHandler> handlerStack))
            {
                lock (handlerStack)
                {
                    if (handlerStack.Count > 0)
                    {
                        INodePacketHandler packetHandler = handlerStack.Peek();
                        packetHandler.PacketReceived(node, packet);
                        return;
                    }
                }
            }

            Assumed.Unreachable($"PacketReceived: no handler for node {node}, unexpected packet type {packet.Type}");
        }

        #endregion

        /// <summary>
        /// Static factory for component creation.
        /// </summary>
        internal static IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            Assumed.Equal(componentType, BuildComponentType.OutOfProcTaskHostNodeProvider, $"Factory cannot create components of type {componentType}");
            return new NodeProviderOutOfProcTaskHost();
        }

        /// <summary>
        /// Factory for the provider shared by every build a worker node process serves.
        /// </summary>
        /// <remarks>
        /// A task host launched with node reuse stays connected to the process that launched it, so
        /// its connection is a process-lifetime resource. A worker node builds a fresh component
        /// collection per build, so resolving a new provider each time would forget task hosts that
        /// are still running and still connected: this process could no longer reach them, and no
        /// other process could claim them either, because a task host pipe accepts only one
        /// connection at a time. That strands one task host per worker per build and loses reuse
        /// entirely, so the object owning those connections is scoped to the process.
        /// </remarks>
        internal static IBuildComponent CreateProcessWideComponent(BuildComponentType componentType)
        {
            Assumed.Equal(componentType, BuildComponentType.OutOfProcTaskHostNodeProvider, $"Factory cannot create components of type {componentType}");
            return s_processWideInstance ??= new NodeProviderOutOfProcTaskHost(processWide: true);
        }

        /// <summary>
        /// Clears out our cached values for the various task host names and paths.
        /// FOR UNIT TESTING ONLY. Must not be called concurrently with methods that
        /// read or populate these statics (e.g. GetMSBuildExecutablePathForNonNETRuntimes),
        /// otherwise cleared fields may be partially reinstated by an in-flight caller.
        /// </summary>
        internal static void ClearCachedTaskHostPaths()
        {
            s_msbuildName = null;
            s_msbuildTaskHostName = null;
            s_pathToX32Clr2 = null;
            s_pathToX32Clr4 = null;
            s_pathToX64Clr2 = null;
            s_pathToX64Clr4 = null;
            s_pathToArm64Clr4 = null;
            s_baseTaskHostPath = null;
            s_baseTaskHostPath64 = null;
            s_baseTaskHostPathArm64 = null;
        }

        /// <summary>
        /// Given a task-host request, returns the name of the executable we should be searching for.
        /// </summary>
        internal static string GetTaskHostNameFromHostContext(TaskHostRequest request)
            => request switch
            {
                Clr2TaskHostRequest => TaskHostNameForClr2TaskHost,
                NetTaskHostRequest or FrameworkTaskHostRequest => GetMSBuildExecutableName(),
            };

        /// <summary>
        /// Given a .NET Framework task-host request, returns the name of the executable we should be searching for.
        /// </summary>
        private static string GetTaskHostNameFromHostContext(ClrTaskHostRequest request)
            => request switch
            {
                Clr2TaskHostRequest => TaskHostNameForClr2TaskHost,
                FrameworkTaskHostRequest => GetMSBuildExecutableName(),
            };

        private static string GetMSBuildExecutableName()
        {
            string name = s_msbuildName;
            if (string.IsNullOrEmpty(name))
            {
                name = Environment.GetEnvironmentVariable("MSBUILD_EXE_NAME");
                if (string.IsNullOrEmpty(name))
                {
                    // Default based on whether it's .NET or Framework
                    name = Constants.MSBuildExecutableName;
                }

                s_msbuildName = name;
            }

            return name;
        }

        /// <summary>
        /// Given a .NET Framework task-host request, returns the appropriate MSBuild executable path.
        /// </summary>
        /// <param name="request">The desired task-host runtime and architecture.</param>
        /// <returns>
        /// The full path to the task-host executable, or <see langword="null"/> when no single Visual Studio installation provides it.
        /// </returns>
        internal static string GetMSBuildExecutablePathForNonNETRuntimes(ClrTaskHostRequest request)
        {
#if RUNTIME_TYPE_NETCORE
            // Not cached: the Developer Command Prompt selection depends on environment variables.
            switch (ResolveVisualStudioTaskHost(request))
            {
                case VisualStudioTaskHostResolution.Found found:
                    return found.TaskHostExecutablePath;
                case VisualStudioTaskHostResolution.NotFound or VisualStudioTaskHostResolution.MultipleFound:
                    return null;
            }
#endif

            string toolName = GetTaskHostNameFromHostContext(request);
            switch (request)
            {
                case Clr2TaskHostRequest clr2Request:
                    Assumed.False(clr2Request.Options.Architecture == TaskHostArchitecture.Arm64, "ARM64 CLR2 task hosts are not supported.");

                    return clr2Request.Options.Architecture == TaskHostArchitecture.X64
                        ? Path.Combine(GetOrInitializeX64Clr2Path(toolName, GetBaseTaskHostPath64()), toolName)
                        : Path.Combine(GetOrInitializeX32Clr2Path(toolName, GetBaseTaskHostPath()), toolName);

                case FrameworkTaskHostRequest frameworkRequest:
                    return frameworkRequest.Options.Architecture switch
                    {
                        TaskHostArchitecture.X64 => Path.Combine(s_pathToX64Clr4 ??= GetBaseTaskHostPath64(), toolName),
                        TaskHostArchitecture.Arm64 => Path.Combine(s_pathToArm64Clr4 ??= GetBaseTaskHostPathArm64(), toolName),
                        _ => Path.Combine(s_pathToX32Clr4 ??= GetBaseTaskHostPath(), toolName),
                    };

                default:
                    return Assumed.Unreachable<string>("Unsupported .NET Framework task-host request.");
            }
        }

        /// <summary>
        /// Returns the path of this installation's MSBuild executable in its 32-bit tools directory.
        /// </summary>
        /// <remarks>
        /// <see cref="Evaluation.IntrinsicFunctions.DoesTaskHostExist"/> reports whether a .NET task host exists from this path in a .NET
        /// process, where a .NET task host request does not select a .NET Framework installation.
        /// </remarks>
        internal static string GetCurrentInstallationMSBuildExecutablePath()
            => Path.Combine(s_pathToX32Clr4 ??= GetBaseTaskHostPath(), GetMSBuildExecutableName());

        // Snapshot to locals so concurrent callers never read a null static
        // that another thread hasn't written yet. Redundant computation is
        // harmless — BuildEnvironmentHelper properties are deterministic.
        private static string GetBaseTaskHostPath()
        {
            string basePath = s_baseTaskHostPath;
            if (basePath is null)
            {
                basePath = BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32;
                s_baseTaskHostPath = basePath;
            }

            return basePath;
        }

        private static string GetBaseTaskHostPath64()
        {
            string basePath64 = s_baseTaskHostPath64;
            if (basePath64 is null)
            {
                basePath64 = BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64;
                s_baseTaskHostPath64 = basePath64;
            }

            return basePath64;
        }

        private static string GetBaseTaskHostPathArm64()
        {
            string basePathArm64 = s_baseTaskHostPathArm64;
            if (basePathArm64 is null)
            {
                basePathArm64 = BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryArm64;
                s_baseTaskHostPathArm64 = basePathArm64;
            }

            return basePathArm64;
        }

        /// <summary>
        /// Handles the handshake scenario where a .NET task host is requested from a .NET Framework process.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// - RuntimeHostPath: The path to the dotnet executable that will host the .NET runtime
        /// - MSBuildPath: The path to MSBuild.dll/MSBuild app host.
        /// </returns>
        internal static (string RuntimeHostPath, string MSBuildPath) GetMSBuildLocationForNETRuntime(NetTaskHostRequest request)
        {
            return (request.LaunchPaths.DotnetHostPath, GetMSBuildPath(request.LaunchPaths.MSBuildAssemblyPath));
        }

        private static string GetMSBuildPath(string msBuildAssemblyPath)
        {
            if (msBuildAssemblyPath != null)
            {
                ValidateNetHostSdkVersion(msBuildAssemblyPath);

                return msBuildAssemblyPath;
            }

#if NET
            // In .NET we resolve the full path based on the tools directory that points to the directory with App Host
            return BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory;
#else
            throw new InvalidProjectFileException(ResourceUtilities.GetResourceString("NETHostTaskLoad_Failed"));
#endif

            static void ValidateNetHostSdkVersion(string path)
            {
                const int MinimumSdkVersion = 10;

                if (string.IsNullOrEmpty(path))
                {
                    InternalError.Throw(ResourceUtilities.GetResourceString("SDKPathResolution_Failed"));
                }

                if (!FileSystems.Default.DirectoryExists(path))
                {
                    InternalError.Throw(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("SDKPathCheck_Failed", path));
                }

                var sdkVersion = ExtractSdkVersionFromPath(path);
                if (sdkVersion is null or < MinimumSdkVersion)
                {
                    throw new InvalidProjectFileException(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("NETHostVersion_Failed", sdkVersion, MinimumSdkVersion));
                }
            }
        }

        /// <summary>
        /// Extracts the major version number from an SDK directory path by parsing the last directory name.
        /// </summary>
        /// <param name="path">
        /// The full path to an SDK directory.
        /// Example: "C:\Program Files\dotnet\sdk\10.0.100-preview.7.25322.101".
        /// </param>
        /// <returns>
        /// The major version number if successfully parsed from the directory name, otherwise null.
        /// For the example path above, this would return 10.
        /// </returns>
        /// <remarks>
        /// The method works by:
        /// 1. Extracting the last directory name from the path (e.g., "10.0.100-preview.7.25322.101")
        /// 2. Finding the first dot in that directory name
        /// 3. Parsing the substring before the first dot as an integer (the major version)
        ///
        /// Returns null if the path is invalid, the last directory name is empty,
        /// there's no dot in the directory name, or the major version cannot be parsed as an integer.
        /// </remarks>
        private static int? ExtractSdkVersionFromPath(string path)
        {
            string lastDirectoryName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));

            if (string.IsNullOrEmpty(lastDirectoryName))
            {
                return null;
            }

            int dotIndex = lastDirectoryName.IndexOf('.');
            if (dotIndex <= 0)
            {
                return null;
            }

            return int.TryParse(lastDirectoryName.Substring(0, dotIndex), out int majorVersion)
                ? majorVersion
                : null;
        }

        private static string GetOrInitializeX64Clr2Path(string toolName, string basePath64)
        {
            return s_pathToX64Clr2 ??= GetPathFromEnvironmentOrDefault("MSBUILDTASKHOSTLOCATION64", basePath64, toolName);
        }

        private static string GetOrInitializeX32Clr2Path(string toolName, string basePath)
        {
            return s_pathToX32Clr2 ??= GetPathFromEnvironmentOrDefault("MSBUILDTASKHOSTLOCATION", basePath, toolName);
        }

        private static string GetPathFromEnvironmentOrDefault(string environmentVariable, string defaultPath, string toolName)
        {
            string envPath = Environment.GetEnvironmentVariable(environmentVariable);

            if (!string.IsNullOrEmpty(envPath))
            {
                string fullPath = Path.Combine(envPath, toolName);
                if (FileUtilities.FileExistsNoThrow(fullPath))
                {
                    return envPath;
                }
            }

            return defaultPath;
        }

        /// <summary>
        /// Make sure a node in the requested context exists, validate its negotiated capabilities,
        /// and configure it for the task.
        /// </summary>
        internal bool AcquireAndSetUpHost(
            TaskHostRequest request,
            int scheduledNodeId,
            bool forwardConsoleOutput,
            INodePacketFactory factory,
            INodePacketHandler handler,
            TaskHostConfiguration configuration,
            bool requiresParameterConversion,
            out bool parameterConversionUnsupported,
            out int hostProcessId,
            out bool wasNewlyCreated,
            out NodeContext<TaskHostHandshake> connection,
            out VisualStudioTaskHostResolution visualStudioResolution)
        {
            parameterConversionUnsupported = false;
            hostProcessId = -1;
            wasNewlyCreated = false;
            connection = null;
            visualStudioResolution = VisualStudioTaskHostResolution.NotApplicable.Instance;
            TaskHostNodeKey requestedNodeKey = new(request, scheduledNodeId, forwardConsoleOutput);
            TaskHostNodeKey nodeKey = default;
            TaskHostLaunchPaths launchPaths = request.LaunchPaths;

            if (launchPaths.MSBuildAssemblyPath is null && launchPaths.DotnetHostPath is null)
            {
                KeyValuePair<TaskHostNodeKey, NodeContext<TaskHostHandshake>>? reusableConnection = null;
                foreach (KeyValuePair<TaskHostNodeKey, NodeContext<TaskHostHandshake>> existing in _nodeContexts)
                {
                    // A nested request without explicit launch paths may reuse only its unique active outer host.
                    if ((existing.Key with { LaunchIdentity = requestedNodeKey.LaunchIdentity }) == requestedNodeKey
                        && HasActiveTaskHandler(existing.Value.NodeId))
                    {
                        if (reusableConnection.HasValue)
                        {
                            reusableConnection = null;
                            break;
                        }

                        reusableConnection = existing;
                    }
                }

                if (reusableConnection is { } reusable)
                {
                    nodeKey = reusable.Key;
                }
            }

            bool HasActiveTaskHandler(int nodeId)
            {
                if (!_nodeIdToPacketHandlerStack.TryGetValue(nodeId, out Stack<INodePacketHandler> handlers))
                {
                    return false;
                }

                lock (handlers)
                {
                    return handlers.Count > 0;
                }
            }

            NodeLaunchData<TaskHostHandshake> nodeLaunchData = default;
            if (nodeKey == default)
            {
                nodeLaunchData = ResolveNodeLaunchConfiguration(request, out visualStudioResolution);
                if (nodeLaunchData.MSBuildLocation is null)
                {
                    return false;
                }

                nodeKey = requestedNodeKey with
                {
                    LaunchIdentity = CreateLaunchIdentity(nodeLaunchData, launchPaths.DotnetHostPath),
                };
            }

            if (!_nodeContexts.ContainsKey(nodeKey))
            {
                if (nodeLaunchData == default)
                {
                    return false;
                }

                wasNewlyCreated = true;
                if (!CreateNode(nodeKey, factory, nodeLaunchData))
                {
                    return false;
                }
            }

            if (!_nodeContexts.TryGetValue(nodeKey, out NodeContext<TaskHostHandshake> context))
            {
                return false;
            }

            if (requiresParameterConversion
                && context.NegotiatedPacketVersion < NodePacketTypeExtensions.TaskParameterConversionMinVersion)
            {
                parameterConversionUnsupported = true;
                return false;
            }

            if (!TryAttachTaskHandler(context, handler))
            {
                return false;
            }

            try
            {
                try
                {
                    hostProcessId = context.Process?.Id ?? -1;
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    hostProcessId = -1;
                }

                lock (_consoleForwardingLock)
                {
                    if (!_isShutDown &&
                        nodeKey.ForwardConsoleOutput &&
                        nodeKey.NodeId == NodeManager.FirstMultiThreadedNodeId &&
                        context.NegotiatedPacketVersion >= NodePacketTypeExtensions.ConsoleOutputForwardingMinVersion &&
                        _consoleForwardingNodeIds.Add(context.NodeId))
                    {
                        context.SendData(new TaskHostConsoleConfiguration());
                    }
                }

                context.SendData(configuration);
                connection = context;
                return true;
            }
            catch
            {
                DisconnectFromHost(context, handler);
                throw;
            }
        }

        /// <summary>
        /// Selects the existing handler whose build callback has reacquired the owning node.
        /// A late callback cannot reactivate a retired or replacement connection.
        /// </summary>
        internal bool TryReactivateTaskHandler(NodeContext<TaskHostHandshake> context, INodePacketHandler handler)
        {
            lock (_activeNodes)
            {
                if (!_nodeIdToNodeKey.ContainsKey(context.NodeId) ||
                    !_nodeIdToPacketHandlerStack.TryGetValue(context.NodeId, out Stack<INodePacketHandler> handlerStack))
                {
                    return false;
                }

                lock (handlerStack)
                {
                    if (handlerStack.Count > 0 && ReferenceEquals(handlerStack.Peek(), handler))
                    {
                        return true;
                    }

                    if (!RemoveTaskHandler(handlerStack, handler))
                    {
                        return false;
                    }

                    handlerStack.Push(handler);
                    return true;
                }
            }
        }

        /// <summary>
        /// Expected to be called when TaskHostTask is done with host of the given context.
        /// </summary>
        internal void DisconnectFromHost(NodeContext<TaskHostHandshake> context, INodePacketHandler handler)
        {
            lock (_activeNodes)
            {
                int nodeId = context.NodeId;

                if (_nodeIdToPacketHandlerStack.TryGetValue(nodeId, out Stack<INodePacketHandler> handlerStack))
                {
                    lock (handlerStack)
                    {
                        RemoveTaskHandler(handlerStack, handler);

                        if (handlerStack.Count == 0)
                        {
                            _nodeIdToPacketHandlerStack.TryRemove(nodeId, out _);
                        }
                    }
                }
            }
        }

        private static bool RemoveTaskHandler(Stack<INodePacketHandler> handlerStack, INodePacketHandler handler)
        {
            if (handlerStack.Count == 0)
            {
                return false;
            }

            if (ReferenceEquals(handlerStack.Peek(), handler))
            {
                handlerStack.Pop();
                return true;
            }

            Stack<INodePacketHandler> laterHandlers = new();
            while (handlerStack.Count > 0 && !ReferenceEquals(handlerStack.Peek(), handler))
            {
                laterHandlers.Push(handlerStack.Pop());
            }

            bool removed = handlerStack.Count > 0;
            if (removed)
            {
                handlerStack.Pop();
            }

            while (laterHandlers.Count > 0)
            {
                handlerStack.Push(laterHandlers.Pop());
            }

            return removed;
        }

        /// <summary>
        /// Instantiates a new MSBuild or MSBuildTaskHost process acting as a child node.
        /// </summary>
        private bool CreateNode(TaskHostNodeKey nodeKey, INodePacketFactory factory, NodeLaunchData<TaskHostHandshake> nodeLaunchData)
        {
            ArgumentNullException.ThrowIfNull(factory);
            Assumed.False(_nodeContexts.ContainsKey(nodeKey), "We should not already have a node for this context!  Did we forget to call DisconnectFromHost somewhere?");

            TaskHostRequest request = nodeKey.Request;
            HandshakeOptions hostContext = request.HandshakeOptions;

            // Generate a unique node ID for communication purposes using atomic increment.
            int communicationNodeId = Interlocked.Increment(ref _nextNodeId);

            // Create callbacks that capture the TaskHostNodeKey
            void OnNodeContextCreated(NodeContext<TaskHostHandshake> context) => NodeContextCreated(context, nodeKey);

            CommunicationsUtilities.Trace($"For a host context of {hostContext}, spawning executable from {nodeLaunchData.MSBuildLocation}.");

            IList<NodeContext<TaskHostHandshake>> nodeContexts = GetNodes(
                nodeLaunchData,
                communicationNodeId,
                this,
                OnNodeContextCreated,
                NodeContextTerminated,
                1);

            return nodeContexts.Count == 1;
        }

        private NodeLaunchData<TaskHostHandshake> ResolveNodeLaunchConfiguration(
            TaskHostRequest request,
            out VisualStudioTaskHostResolution visualStudioResolution)
        {
            visualStudioResolution = VisualStudioTaskHostResolution.NotApplicable.Instance;

            if (request is NetTaskHostRequest netRequest)
            {
                return ResolveAppHostOrFallback(netRequest);
            }

#if FEATURE_NET35_TASKHOST
            if (request is Clr2TaskHostRequest clr2Request)
            {
#if RUNTIME_TYPE_NETCORE
                return ResolveVisualStudioTaskHostLaunchConfiguration(clr2Request, out visualStudioResolution);
#else
                string msbuildLocation = GetMSBuildExecutablePathForNonNETRuntimes(clr2Request);
                string toolsDirectory = Path.GetDirectoryName(msbuildLocation) ?? string.Empty;
                return NodeLaunchData.ForClr2TaskHost(clr2Request, msbuildLocation, toolsDirectory);
#endif
            }
#endif

            if (request is not FrameworkTaskHostRequest frameworkRequest)
            {
                return Assumed.Unreachable<NodeLaunchData<TaskHostHandshake>>("Unsupported task-host request.");
            }

#if RUNTIME_TYPE_NETCORE
            return ResolveVisualStudioTaskHostLaunchConfiguration(frameworkRequest, out visualStudioResolution);
#else
            return NodeLaunchData.ForFrameworkTaskHost(
                frameworkRequest,
                GetMSBuildExecutablePathForNonNETRuntimes(frameworkRequest));
#endif
        }

#if RUNTIME_TYPE_NETCORE
        private NodeLaunchData<TaskHostHandshake> ResolveVisualStudioTaskHostLaunchConfiguration(
            Clr2TaskHostRequest request,
            out VisualStudioTaskHostResolution visualStudioResolution)
        {
            HandshakeOptions hostContext = request.HandshakeOptions;
            visualStudioResolution = ResolveVisualStudioTaskHost(request);
            if (visualStudioResolution is not VisualStudioTaskHostResolution.Found found)
            {
                CommunicationsUtilities.Trace($"For a host context of {hostContext}, no single Visual Studio installation was found.");
                return default;
            }

            Version fileVersion = GetFileVersion(found.TaskHostExecutablePath);
            CommunicationsUtilities.Trace($"For a host context of {hostContext}, using Visual Studio task host from {found.TaskHostExecutablePath} (version {fileVersion}).");
            return NodeLaunchData.ForClr2TaskHost(
                request,
                found.TaskHostExecutablePath,
                found.HandshakeToolsDirectory,
                fileVersion);
        }

        private NodeLaunchData<TaskHostHandshake> ResolveVisualStudioTaskHostLaunchConfiguration(
            FrameworkTaskHostRequest request,
            out VisualStudioTaskHostResolution visualStudioResolution)
        {
            HandshakeOptions hostContext = request.HandshakeOptions;
            visualStudioResolution = ResolveVisualStudioTaskHost(request);
            if (visualStudioResolution is not VisualStudioTaskHostResolution.Found found)
            {
                CommunicationsUtilities.Trace($"For a host context of {hostContext}, no single Visual Studio installation was found.");
                return default;
            }

            Version fileVersion = GetFileVersion(found.TaskHostExecutablePath);
            CommunicationsUtilities.Trace($"For a host context of {hostContext}, using Visual Studio MSBuild from {found.TaskHostExecutablePath} (version {fileVersion}).");

            // The .NET Framework task host computes its handshake from its installation. Override
            // MSBUILD_EXE_PATH because the .NET CLI sets it to the SDK's MSBuild.dll.
            return NodeLaunchData.ForExternalFrameworkTaskHost(
                request,
                found.TaskHostExecutablePath,
                found.HandshakeToolsDirectory,
                fileVersion);
        }
#endif

        /// <summary>
        /// The outcome of locating a Visual Studio installation whose task host can run tasks that require
        /// the .NET Framework for .NET MSBuild. Every outcome is one of the nested case types,
        /// and each carries only the information relevant to it.
        /// </summary>
        internal union VisualStudioTaskHostResolution(
            VisualStudioTaskHostResolution.NotApplicable,
            VisualStudioTaskHostResolution.Found,
            VisualStudioTaskHostResolution.NotFound,
            VisualStudioTaskHostResolution.MultipleFound)
        {
            /// <summary>
            /// Visual Studio was not queried: the requested task host is not a .NET Framework task host, or this is not Windows.
            /// </summary>
            internal sealed record NotApplicable
            {
                private NotApplicable()
                {
                }

                internal static NotApplicable Instance { get; } = new();
            }

            /// <summary>
            /// A single Visual Studio installation was selected.
            /// </summary>
            /// <param name="TaskHostExecutablePath">The full path to the executable to launch for the requested runtime and architecture.</param>
            /// <param name="HandshakeToolsDirectory">The directory used by the task host in its handshake.</param>
            internal sealed record Found(string TaskHostExecutablePath, string HandshakeToolsDirectory);

            /// <summary>
            /// No Visual Studio installation with a suitable task-host executable was found.
            /// </summary>
            internal sealed record NotFound
            {
                private NotFound()
                {
                }

                internal static NotFound Instance { get; } = new();
            }

            /// <summary>
            /// Several Visual Studio installations with a suitable task-host executable were found, and the environment did not select one.
            /// </summary>
            /// <param name="Installations">The installations that could serve the request.</param>
            internal sealed record MultipleFound(IReadOnlyList<VisualStudioInstance> Installations);
        }

#if RUNTIME_TYPE_NETCORE
        private static readonly int s_currentVisualStudioMajorVersion = Version.Parse(MSBuildConstants.CurrentVisualStudioVersion).Major;

        /// <summary>
        /// Visual Studio installations reported by the Visual Studio setup API, queried at most once per build.
        /// </summary>
        private static IList<VisualStudioInstance> s_visualStudioInstances;

        /// <summary>
        /// Locates a task host in a Visual Studio installation to run tasks that require the .NET Framework.
        /// The installation of a Visual Studio Developer Command Prompt is preferred; otherwise the
        /// Visual Studio setup API is queried, and an installation is used only when it is the only one available.
        /// </summary>
        /// <param name="request">The typed task-host request.</param>
        internal static VisualStudioTaskHostResolution ResolveVisualStudioTaskHost(ClrTaskHostRequest request)
            => NativeMethodsShared.IsWindows
                ? ResolveVisualStudioTaskHost(request, GetVisualStudioInstances)
                : VisualStudioTaskHostResolution.NotApplicable.Instance;

        /// <summary>
        /// Platform-independent implementation of <see cref="ResolveVisualStudioTaskHost(ClrTaskHostRequest)"/>
        /// with an explicit source of Visual Studio installations.
        /// </summary>
        internal static VisualStudioTaskHostResolution ResolveVisualStudioTaskHost(ClrTaskHostRequest request, Func<IList<VisualStudioInstance>> getVisualStudioInstances)
        {
            // A Developer Command Prompt selects the installation explicitly.
            if (TryGetDeveloperCommandPromptInstallation(out string developerCommandPromptInstallDirectory)
                && FindTaskHostInInstallation(developerCommandPromptInstallDirectory, request) is { } developerCommandPromptTaskHost)
            {
                return developerCommandPromptTaskHost;
            }

            List<VisualStudioInstance> matchingInstallations = [];
            VisualStudioTaskHostResolution.Found matchingTaskHost = null;
            foreach (VisualStudioInstance instance in getVisualStudioInstances() ?? [])
            {
                if (instance.Version?.Major == s_currentVisualStudioMajorVersion
                    && FindTaskHostInInstallation(instance.Path, request) is { } taskHost)
                {
                    matchingInstallations.Add(instance);
                    matchingTaskHost = taskHost;
                }
            }

            return matchingInstallations.Count switch
            {
                0 => VisualStudioTaskHostResolution.NotFound.Instance,
                1 => matchingTaskHost,
                _ => new VisualStudioTaskHostResolution.MultipleFound(matchingInstallations),
            };
        }

        /// <summary>
        /// Formats Visual Studio installations for an error message, one installation per line.
        /// </summary>
        internal static string FormatVisualStudioInstallations(IReadOnlyList<VisualStudioInstance> installations)
        {
            StringBuilder builder = new();
            foreach (VisualStudioInstance installation in installations)
            {
                builder.AppendLine();
                builder.Append("    ").Append(installation.Name).Append(" (").Append(installation.Version).Append("): ").Append(installation.Path);
            }

            return builder.ToString();
        }

        private static IList<VisualStudioInstance> GetVisualStudioInstances()
            => s_visualStudioInstances ??= VisualStudioLocationHelper.GetInstances();

        /// <summary>
        /// Reads the Visual Studio installation directory set by a Developer Command Prompt. Only the Visual Studio
        /// version matching this MSBuild is supported, as BuildEnvironmentHelper does for a Developer Command Prompt.
        /// </summary>
        private static bool TryGetDeveloperCommandPromptInstallation(out string visualStudioInstallDirectory)
        {
            visualStudioInstallDirectory = Environment.GetEnvironmentVariable("VSINSTALLDIR");
            return !string.IsNullOrEmpty(visualStudioInstallDirectory)
                && string.Equals(Environment.GetEnvironmentVariable("VisualStudioVersion"), MSBuildConstants.CurrentVisualStudioVersion, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets the task host for the requested runtime and architecture within a Visual Studio installation.
        /// </summary>
        /// <returns>The task host in the installation, or <see langword="null"/> when it does not exist.</returns>
        private static VisualStudioTaskHostResolution.Found FindTaskHostInInstallation(string visualStudioInstallDirectory, ClrTaskHostRequest request)
        {
            if (string.IsNullOrEmpty(visualStudioInstallDirectory))
            {
                return null;
            }

            string root;
            try
            {
                // Match the tools directory root computed by the task host itself: a full path without a trailing separator.
                root = Path.GetFullPath(Path.Combine(visualStudioInstallDirectory, "MSBuild", MSBuildConstants.CurrentToolsVersion, "Bin"))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                return null;
            }

            TaskHostArchitecture architecture = request.Architecture;
            if (request is Clr2TaskHostRequest && architecture == TaskHostArchitecture.Arm64)
            {
                return null;
            }

            string architectureDirectory = architecture switch
            {
                TaskHostArchitecture.X64 => Path.Combine(root, "amd64"),
                TaskHostArchitecture.Arm64 => Path.Combine(root, "arm64"),
                _ => root,
            };

            // A CLR2 task host computes its handshake from its own directory; MSBuild.exe from the tools directory root.
            (string executableName, string handshakeToolsDirectory) = request switch
            {
                Clr2TaskHostRequest => (TaskHostNameForClr2TaskHost, architectureDirectory),
                FrameworkTaskHostRequest => ("MSBuild.exe", root),
            };

            string msbuildExePath = Path.Combine(architectureDirectory, executableName);
            return FileSystems.Default.FileExists(msbuildExePath)
                ? new VisualStudioTaskHostResolution.Found(msbuildExePath, handshakeToolsDirectory)
                : null;
        }

        private static Version GetFileVersion(string path)
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
            return new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart, versionInfo.FileBuildPart, versionInfo.FilePrivatePart);
        }
#endif

        private static TaskHostLaunchIdentity CreateLaunchIdentity(NodeLaunchData<TaskHostHandshake> launchData, string dotnetHostPath)
            => new(
                NormalizeLaunchPath(launchData.MSBuildLocation),
                launchData.CommandLineArgs ?? string.Empty,
                NormalizeLaunchPath(dotnetHostPath));

        private static string NormalizeLaunchPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalizedPath = FileUtilities.NormalizePath(path);
            return FileUtilities.IsFileSystemCaseSensitive ? normalizedPath : normalizedPath.ToUpperInvariant();
        }

        /// <summary>
        /// Resolves whether to use the MSBuild app host or fall back to dotnet.exe.
        /// </summary>
        /// <param name="request">The typed request for the .NET task host.</param>
        /// <returns>The resolved node launch configuration.</returns>
        private NodeLaunchData<TaskHostHandshake> ResolveAppHostOrFallback(NetTaskHostRequest request)
        {
            string msbuildAssemblyPath = GetMSBuildPath(request.LaunchPaths.MSBuildAssemblyPath);
            HandshakeOptions hostContext = request.HandshakeOptions;
            (string launchPath, bool useAppHost) = ResolveNetTaskHostLaunchPath(msbuildAssemblyPath);

            // The child task host (NodeEndpointOutOfProcTaskHost) computes its handshake
            // toolsDirectory from BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot,
            // which derives from AppContext.BaseDirectory (resolves symlinks).
            //
            // On .NET Framework, the parent MSBuild (VS) is in a different directory than the
            // child .NET task host (SDK), so we must pass msbuildAssemblyPath explicitly to
            // match the child's location. Windows has no symlink issues so this is safe.
            //
            // On .NET Core, parent and child are always from the same SDK directory. Passing
            // msbuildAssemblyPath from $(NetCoreSdkRoot) can cause a handshake mismatch on
            // macOS where /tmp -> /private/tmp symlink means the property value differs from
            // AppContext.BaseDirectory. By omitting toolsDirectory, both sides default to
            // BuildEnvironmentHelper which resolves symlinks consistently.
#if RUNTIME_TYPE_NETCORE
            string toolsDirectory = null;
#else
            string toolsDirectory = msbuildAssemblyPath;
#endif

            if (useAppHost)
            {
                CommunicationsUtilities.Trace($"For a host context of {hostContext}, using app host from {launchPath}.");

                return NodeLaunchData.ForNetTaskHostAppHost(
                    request,
                    launchPath,
                    toolsDirectory);
            }

            NodeLaunchData<TaskHostHandshake> launch = NodeLaunchData.ForNetTaskHostDll(
                request,
                launchPath,
                toolsDirectory);
            CommunicationsUtilities.Trace($"For a host context of {hostContext}, app host not found, falling back to dotnet.exe ({launch.MSBuildLocation}) hosting {launchPath}.");
            return launch;
        }

        /// <summary>
        /// Resolves the .NET task host launch target for an SDK directory: the MSBuild app host
        /// (<c>MSBuild[.exe]</c>) when present, otherwise <c>MSBuild.dll</c> (which is launched via
        /// <c>dotnet[.exe]</c>). Single source of truth for the apphost-vs-fallback decision,
        /// shared by the launch path and any caller that needs to describe the launch target
        /// (e.g. error messages).
        /// </summary>
        internal static (string LaunchPath, bool UseAppHost) ResolveNetTaskHostLaunchPath(string msbuildAssemblyPath)
        {
            string appHostPath = Path.Combine(msbuildAssemblyPath, Constants.MSBuildExecutableName);
            return FileSystems.Default.FileExists(appHostPath)
                ? (appHostPath, true)
                : (Path.Combine(msbuildAssemblyPath, Constants.MSBuildAssemblyName), false);
        }

        /// <summary>
        /// Method called when a context created.
        /// </summary>
        internal void NodeContextCreated(NodeContext<TaskHostHandshake> context, TaskHostNodeKey nodeKey)
        {
            lock (_activeNodes)
            {
                _nodeContexts[nodeKey] = context;
                _nodeIdToNodeKey[context.NodeId] = nodeKey;
                TryActivateNode(context);
            }

            // Start the asynchronous read.
            context.BeginAsyncPacketRead();
        }

        /// <summary>
        /// Marks a node as participating in the current build, so that shutdown waits for it.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> if the node is no longer tracked, meaning its process went away
        /// between being looked up and being used, in which case the caller must not use it.
        /// </returns>
        private bool TryActivateNode(NodeContext<TaskHostHandshake> context)
        {
            lock (_activeNodes)
            {
                if (!_nodeIdToNodeKey.ContainsKey(context.NodeId))
                {
                    return false;
                }

                if (_activeNodes.Add(context.NodeId))
                {
                    _noNodesActiveEvent.Reset();
                }

                return true;
            }
        }

        internal bool TryAttachTaskHandler(NodeContext<TaskHostHandshake> context, INodePacketHandler handler)
        {
            lock (_activeNodes)
            {
                if (!TryActivateNode(context))
                {
                    return false;
                }

                Stack<INodePacketHandler> handlers = _nodeIdToPacketHandlerStack.GetOrAdd(context.NodeId, _ => new Stack<INodePacketHandler>());
                lock (handlers)
                {
                    handlers.Push(handler);
                }
                return true;
            }
        }

        internal int TaskHandlerRegistrationCount => _nodeIdToPacketHandlerStack.Count;

        /// <summary>
        /// Method called when a context terminates (called from CreateNode callbacks or ShutdownAllNodes).
        /// </summary>
        internal void NodeContextTerminated(int nodeId)
        {
            lock (_consoleForwardingLock)
            {
                _consoleForwardingNodeIds.Remove(nodeId);
            }

            lock (_activeNodes)
            {
                RetireNode(nodeId);
                _nodeIdToPacketHandlerStack.TryRemove(nodeId, out _);
            }
        }

        // Stop acquisition and shutdown waits, but keep handlers until the connection notifies them.
        private void RetireNode(int nodeId)
        {
            lock (_activeNodes)
            {
                if (_nodeIdToNodeKey.TryRemove(nodeId, out TaskHostNodeKey nodeKey))
                {
                    _nodeContexts.TryRemove(nodeKey, out _);
                }
                _activeNodes.Remove(nodeId);

                if (_activeNodes.Count == 0)
                {
                    _noNodesActiveEvent.Set();
                }
            }
        }

        public IEnumerable<Process> GetProcesses() => _nodeContexts.Values.Select(context => context.Process);
    }
}
