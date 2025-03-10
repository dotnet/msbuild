// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The provider for out-of-proc nodes.  This manages the lifetime of external MSBuild.exe processes
    /// which act as child nodes for the build system.
    /// </summary>
    internal class NodeProviderOutOfProcTaskHost : NodeProviderOutOfProcBase, INodeProvider, INodePacketFactory, INodePacketHandler
    {
        /// <summary>
        /// The maximum number of nodes that this provider supports. Should
        /// always be equivalent to the number of different TaskHostContexts
        /// that exist.
        /// </summary>
        private const int MaxNodeCount = 4;

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
        /// A mapping of all the nodes managed by this provider.
        /// </summary>
        private Dictionary<HandshakeOptions, NodeContext> _nodeContexts;

        /// <summary>
        /// A mapping of all of the INodePacketFactories wrapped by this provider.
        /// </summary>
        private IDictionary<int, INodePacketFactory> _nodeIdToPacketFactory;

        /// <summary>
        /// A mapping of all of the INodePacketHandlers wrapped by this provider.
        /// </summary>
        private IDictionary<int, INodePacketHandler> _nodeIdToPacketHandler;

        /// <summary>
        /// Keeps track of the set of nodes for which we have not yet received shutdown notification.
        /// </summary>
        private HashSet<int> _activeNodes;

        /// <summary>
        /// Packet factory we use if there's not already one associated with a particular context.
        /// </summary>
        private NodePacketFactory _localPacketFactory;

        /// <summary>
        /// Constructor.
        /// </summary>
        private NodeProviderOutOfProcTaskHost()
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
                return MaxNodeCount - _nodeContexts.Count;
            }
        }

        /// <summary>
        /// Returns the name of the CLR2 Task Host executable
        /// </summary>
        internal static string TaskHostNameForClr2TaskHost
        {
            get
            {
                if (s_msbuildTaskHostName == null)
                {
                    s_msbuildTaskHostName = Environment.GetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME");

                    if (s_msbuildTaskHostName == null)
                    {
                        s_msbuildTaskHostName = "MSBuildTaskHost.exe";
                    }
                }

                return s_msbuildTaskHostName;
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
        /// </summary>
        /// <param name="nodeId">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(int nodeId, INodePacket packet)
        {
            throw new NotImplementedException("Use the other overload of SendData instead");
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="hostContext">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(HandshakeOptions hostContext, INodePacket packet)
        {
            ErrorUtilities.VerifyThrow(_nodeContexts.ContainsKey(hostContext), "Invalid host context specified: {0}.", hostContext.ToString());

            SendData(_nodeContexts[hostContext], packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            // Send the build completion message to the nodes, causing them to shutdown or reset.
            List<NodeContext> contextsToShutDown;

            lock (_nodeContexts)
            {
                contextsToShutDown = new List<NodeContext>(_nodeContexts.Values);
            }

            ShutdownConnectedNodes(contextsToShutDown, enableReuse);

            _noNodesActiveEvent.WaitOne();
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
            ShutdownAllNodes(ComponentHost.BuildParameters.EnableNodeReuse, NodeContextTerminated);
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
            _nodeContexts = new Dictionary<HandshakeOptions, NodeContext>();
            _nodeIdToPacketFactory = new Dictionary<int, INodePacketFactory>();
            _nodeIdToPacketHandler = new Dictionary<int, INodePacketHandler>();
            _activeNodes = new HashSet<int>();

            _noNodesActiveEvent = new ManualResetEvent(true);
            _localPacketFactory = new NodePacketFactory();

            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.LogMessage, LogMessagePacket.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.TaskHostTaskComplete, TaskHostTaskComplete.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, this);
        }

        /// <summary>
        /// Shuts down the component
        /// </summary>
        public void ShutdownComponent()
        {
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
        /// Takes a serializer and deserializes the packet.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator)
        {
            return _localPacketFactory.DeserializePacket(packetType, translator);
        }

        /// <summary>
        /// Routes the specified packet
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            if (_nodeIdToPacketFactory.TryGetValue(nodeId, out INodePacketFactory nodePacketFactory))
            {
                nodePacketFactory.RoutePacket(nodeId, packet);
            }
            else
            {
                _localPacketFactory.RoutePacket(nodeId, packet);
            }
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
            if (_nodeIdToPacketHandler.TryGetValue(node, out INodePacketHandler packetHandler))
            {
                packetHandler.PacketReceived(node, packet);
            }
            else
            {
                ErrorUtilities.VerifyThrow(packet.Type == NodePacketType.NodeShutdown, "We should only ever handle packets of type NodeShutdown -- everything else should only come in when there's an active task");

                // May also be removed by unnatural termination, so don't assume it's there
                lock (_activeNodes)
                {
                    if (_activeNodes.Contains(node))
                    {
                        _activeNodes.Remove(node);
                    }

                    if (_activeNodes.Count == 0)
                    {
                        _noNodesActiveEvent.Set();
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Static factory for component creation.
        /// </summary>
        internal static IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            ErrorUtilities.VerifyThrow(componentType == BuildComponentType.OutOfProcTaskHostNodeProvider, "Factory cannot create components of type {0}", componentType);
            return new NodeProviderOutOfProcTaskHost();
        }

        /// <summary>
        /// Clears out our cached values for the various task host names and paths.
        /// FOR UNIT TESTING ONLY
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
        /// Given a TaskHostContext, returns the name of the executable we should be searching for.
        /// </summary>
        internal static string GetTaskHostNameFromHostContext(HandshakeOptions hostContext)
        {
            ErrorUtilities.VerifyThrowInternalErrorUnreachable((hostContext & HandshakeOptions.TaskHost) == HandshakeOptions.TaskHost);
            if ((hostContext & HandshakeOptions.CLR2) == HandshakeOptions.CLR2)
            {
                return TaskHostNameForClr2TaskHost;
            }
            else
            {
                if (s_msbuildName == null)
                {
                    s_msbuildName = Environment.GetEnvironmentVariable("MSBUILD_EXE_NAME");

                    if (s_msbuildName == null)
                    {
                        s_msbuildName = (hostContext & HandshakeOptions.NET) == HandshakeOptions.NET
                            ? "MSBuild.dll"
                            : "MSBuild.exe";
                    }
                }

                return s_msbuildName;
            }
        }

        /// <summary>
        /// Given a TaskHostContext, return the appropriate location of the
        /// executable (MSBuild or MSBuildTaskHost) that we wish to use, or null
        /// if that location cannot be resolved.
        /// </summary>
        internal static string GetMSBuildLocationFromHostContext(HandshakeOptions hostContext)
        {
            string toolName = GetTaskHostNameFromHostContext(hostContext);
            string toolPath = null;

            s_baseTaskHostPath = BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32;
            s_baseTaskHostPath64 = BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64;
            s_baseTaskHostPathArm64 = BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryArm64;

            ErrorUtilities.VerifyThrowInternalErrorUnreachable((hostContext & HandshakeOptions.TaskHost) == HandshakeOptions.TaskHost);

            if ((hostContext & HandshakeOptions.Arm64) == HandshakeOptions.Arm64 && (hostContext & HandshakeOptions.CLR2) == HandshakeOptions.CLR2)
            {
                // Unsupported, throw.
                ErrorUtilities.ThrowInternalError("ARM64 CLR2 task hosts are not supported.");
            }
            else if ((hostContext & HandshakeOptions.X64) == HandshakeOptions.X64 && (hostContext & HandshakeOptions.CLR2) == HandshakeOptions.CLR2)
            {
                if (s_pathToX64Clr2 == null)
                {
                    s_pathToX64Clr2 = Environment.GetEnvironmentVariable("MSBUILDTASKHOSTLOCATION64");

                    if (s_pathToX64Clr2 == null || !FileUtilities.FileExistsNoThrow(Path.Combine(s_pathToX64Clr2, toolName)))
                    {
                        s_pathToX64Clr2 = s_baseTaskHostPath64;
                    }
                }

                toolPath = s_pathToX64Clr2;
            }
            else if ((hostContext & HandshakeOptions.CLR2) == HandshakeOptions.CLR2)
            {
                if (s_pathToX32Clr2 == null)
                {
                    s_pathToX32Clr2 = Environment.GetEnvironmentVariable("MSBUILDTASKHOSTLOCATION");
                    if (s_pathToX32Clr2 == null || !FileUtilities.FileExistsNoThrow(Path.Combine(s_pathToX32Clr2, toolName)))
                    {
                        s_pathToX32Clr2 = s_baseTaskHostPath;
                    }
                }

                toolPath = s_pathToX32Clr2;
            }
            else if ((hostContext & HandshakeOptions.X64) == HandshakeOptions.X64)
            {
                if (s_pathToX64Clr4 == null)
                {
                    s_pathToX64Clr4 = s_baseTaskHostPath64;
                }

                toolPath = s_pathToX64Clr4;
            }
            else if ((hostContext & HandshakeOptions.Arm64) == HandshakeOptions.Arm64)
            {
                if (s_pathToArm64Clr4 == null)
                {
                    s_pathToArm64Clr4 = s_baseTaskHostPathArm64;
                }

                toolPath = s_pathToArm64Clr4;
            }
            else
            {
                if (s_pathToX32Clr4 == null)
                {
                    s_pathToX32Clr4 = s_baseTaskHostPath;
                }

                toolPath = s_pathToX32Clr4;
            }

            if (toolName != null && toolPath != null)
            {
                return Path.Combine(toolPath, toolName);
            }

            return null;
        }

        /// <summary>
        /// Make sure a node in the requested context exists.
        /// </summary>
        internal bool AcquireAndSetUpHost(HandshakeOptions hostContext, INodePacketFactory factory, INodePacketHandler handler, TaskHostConfiguration configuration)
        {
            bool nodeCreationSucceeded;
            if (!_nodeContexts.ContainsKey(hostContext))
            {
                nodeCreationSucceeded = CreateNode(hostContext, factory, handler, configuration);
            }
            else
            {
                // node already exists, so "creation" automatically succeeded
                nodeCreationSucceeded = true;
            }

            if (nodeCreationSucceeded)
            {
                NodeContext context = _nodeContexts[hostContext];
                _nodeIdToPacketFactory[(int)hostContext] = factory;
                _nodeIdToPacketHandler[(int)hostContext] = handler;

                // Configure the node.
                context.SendData(configuration);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Expected to be called when TaskHostTask is done with host of the given context.
        /// </summary>
        internal void DisconnectFromHost(HandshakeOptions hostContext)
        {
            ErrorUtilities.VerifyThrow(_nodeIdToPacketFactory.ContainsKey((int)hostContext) && _nodeIdToPacketHandler.ContainsKey((int)hostContext), "Why are we trying to disconnect from a context that we already disconnected from?  Did we call DisconnectFromHost twice?");

            _nodeIdToPacketFactory.Remove((int)hostContext);
            _nodeIdToPacketHandler.Remove((int)hostContext);
        }

        /// <summary>
        /// Instantiates a new MSBuild or MSBuildTaskHost process acting as a child node.
        /// </summary>
        internal bool CreateNode(HandshakeOptions hostContext, INodePacketFactory factory, INodePacketHandler handler, TaskHostConfiguration configuration)
        {
            ErrorUtilities.VerifyThrowArgumentNull(factory);
            ErrorUtilities.VerifyThrow(!_nodeIdToPacketFactory.ContainsKey((int)hostContext), "We should not already have a factory for this context!  Did we forget to call DisconnectFromHost somewhere?");

            if (AvailableNodes <= 0)
            {
                ErrorUtilities.ThrowInternalError("All allowable nodes already created ({0}).", _nodeContexts.Count);
                return false;
            }

            // Start the new process.  We pass in a node mode with a node number of 2, to indicate that we
            // want to start up an MSBuild task host node.
            string commandLineArgs = $" /nologo /nodemode:2 /nodereuse:{ComponentHost.BuildParameters.EnableNodeReuse} /low:{ComponentHost.BuildParameters.LowPriority} ";

            string msbuildLocation = GetMSBuildLocationFromHostContext(hostContext);

            // we couldn't even figure out the location we're trying to launch ... just go ahead and fail.
            if (msbuildLocation == null)
            {
                return false;
            }

            CommunicationsUtilities.Trace("For a host context of {0}, spawning executable from {1}.", hostContext.ToString(), msbuildLocation ?? "MSBuild.exe");

            // There is always one task host per host context so we always create just 1 one task host node here.
            int nodeId = (int)hostContext;
            IList<NodeContext> nodeContexts = GetNodes(
                msbuildLocation,
                commandLineArgs,
                nodeId,
                this,
                new Handshake(hostContext),
                NodeContextCreated,
                NodeContextTerminated,
                1);

            return nodeContexts.Count == 1;
        }

        /// <summary>
        /// Method called when a context created.
        /// </summary>
        private void NodeContextCreated(NodeContext context)
        {
            _nodeContexts[(HandshakeOptions)context.NodeId] = context;

            // Start the asynchronous read.
            context.BeginAsyncPacketRead();

            lock (_activeNodes)
            {
                _activeNodes.Add(context.NodeId);
            }
            _noNodesActiveEvent.Reset();
        }

        /// <summary>
        /// Method called when a context terminates.
        /// </summary>
        private void NodeContextTerminated(int nodeId)
        {
            lock (_nodeContexts)
            {
                _nodeContexts.Remove((HandshakeOptions)nodeId);
            }

            // May also be removed by unnatural termination, so don't assume it's there
            lock (_activeNodes)
            {
                if (_activeNodes.Contains(nodeId))
                {
                    _activeNodes.Remove(nodeId);
                }

                if (_activeNodes.Count == 0)
                {
                    _noNodesActiveEvent.Set();
                }
            }
        }

        public IEnumerable<Process> GetProcesses()
        {
            return _nodeContexts.Values.Select(context => context.Process);
        }
    }
}
