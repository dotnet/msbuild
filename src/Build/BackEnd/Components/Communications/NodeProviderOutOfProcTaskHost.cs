// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.AccessControl;
#endif
using System.Security.Principal;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

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
        private Dictionary<TaskHostContext, NodeContext> _nodeContexts;

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
        public bool CreateNode(int nodeId, INodePacketFactory factory, NodeConfiguration configuration)
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
        public void SendData(TaskHostContext hostContext, INodePacket packet)
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
            ShutdownAllNodes(NodeProviderOutOfProc.GetHostHandshake(ComponentHost.BuildParameters.EnableNodeReuse), NodeProviderOutOfProc.GetClientHandshake(), NodeContextTerminated);
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
            _nodeContexts = new Dictionary<TaskHostContext, NodeContext>();
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
        /// Takes a serializer, deserializes the packet and routes it to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, INodePacketTranslator translator)
        {
            if (_nodeIdToPacketFactory.ContainsKey(nodeId))
            {
                _nodeIdToPacketFactory[nodeId].DeserializeAndRoutePacket(nodeId, packetType, translator);
            }
            else
            {
                _localPacketFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
            }
        }

        /// <summary>
        /// Routes the specified packet
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            if (_nodeIdToPacketFactory.ContainsKey(nodeId))
            {
                _nodeIdToPacketFactory[nodeId].RoutePacket(nodeId, packet);
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
            if (_nodeIdToPacketHandler.ContainsKey(node))
            {
                _nodeIdToPacketHandler[node].PacketReceived(node, packet);
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
        static internal IBuildComponent CreateComponent(BuildComponentType componentType)
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
            s_baseTaskHostPath = null;
            s_baseTaskHostPath64 = null;
        }

        /// <summary>
        /// Given a TaskHostContext, returns the name of the executable we should be searching for. 
        /// </summary>
        internal static string GetTaskHostNameFromHostContext(TaskHostContext hostContext)
        {
            if (hostContext == TaskHostContext.X64CLR4 || hostContext == TaskHostContext.X32CLR4)
            {
                if (s_msbuildName == null)
                {
                    s_msbuildName = Environment.GetEnvironmentVariable("MSBUILD_EXE_NAME");

                    if (s_msbuildName == null)
                    {
                        s_msbuildName = "MSBuild.exe";
                    }
                }

                return s_msbuildName;
            }
            else if (hostContext == TaskHostContext.X32CLR2 || hostContext == TaskHostContext.X64CLR2)
            {
                return TaskHostNameForClr2TaskHost;
            }
            else
            {
                ErrorUtilities.ThrowInternalErrorUnreachable();
                return null;
            }
        }

        /// <summary>
        /// Given a TaskHostContext, return the appropriate location of the 
        /// executable (MSBuild or MSBuildTaskHost) that we wish to use, or null
        /// if that location cannot be resolved.  
        /// </summary>
        internal static string GetMSBuildLocationFromHostContext(TaskHostContext hostContext)
        {
            string toolName = GetTaskHostNameFromHostContext(hostContext);
            string toolPath = null;

            s_baseTaskHostPath = BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32;
            s_baseTaskHostPath64 = BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64;

            switch (hostContext)
            {
                case TaskHostContext.X32CLR2:
                    if (s_pathToX32Clr2 == null)
                    {
                        s_pathToX32Clr2 = Environment.GetEnvironmentVariable("MSBUILDTASKHOSTLOCATION");
                        if (s_pathToX32Clr2 == null || !FileUtilities.FileExistsNoThrow(Path.Combine(s_pathToX32Clr2, toolName)))
                        {
                            s_pathToX32Clr2 = s_baseTaskHostPath;
                        }
                    }

                    toolPath = s_pathToX32Clr2;
                    break;
                case TaskHostContext.X64CLR2:
                    if (s_pathToX64Clr2 == null)
                    {
                        s_pathToX64Clr2 = Environment.GetEnvironmentVariable("MSBUILDTASKHOSTLOCATION64");

                        if (s_pathToX64Clr2 == null || !FileUtilities.FileExistsNoThrow(Path.Combine(s_pathToX64Clr2, toolName)))
                        {
                            s_pathToX64Clr2 = s_baseTaskHostPath64;
                        }
                    }

                    toolPath = s_pathToX64Clr2;
                    break;
                case TaskHostContext.X32CLR4:
                    if (s_pathToX32Clr4 == null)
                    {
                        s_pathToX32Clr4 = s_baseTaskHostPath;
                    }

                    toolPath = s_pathToX32Clr4;
                    break;
                case TaskHostContext.X64CLR4:
                    if (s_pathToX64Clr4 == null)
                    {
                        s_pathToX64Clr4 = s_baseTaskHostPath64;
                    }

                    toolPath = s_pathToX64Clr4;
                    break;
                default:
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    break;
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
        internal bool AcquireAndSetUpHost(TaskHostContext hostContext, INodePacketFactory factory, INodePacketHandler handler, TaskHostConfiguration configuration)
        {
            NodeContext context = null;
            bool nodeCreationSucceeded = false;

            if (!(_nodeContexts.TryGetValue(hostContext, out context)))
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
                context = _nodeContexts[hostContext];
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
        internal void DisconnectFromHost(TaskHostContext hostContext)
        {
            ErrorUtilities.VerifyThrow(_nodeIdToPacketFactory.ContainsKey((int)hostContext) && _nodeIdToPacketHandler.ContainsKey((int)hostContext), "Why are we trying to disconnect from a context that we already disconnected from?  Did we call DisconnectFromHost twice?");

            _nodeIdToPacketFactory.Remove((int)hostContext);
            _nodeIdToPacketHandler.Remove((int)hostContext);
        }

        /// <summary>
        /// Instantiates a new MSBuild or MSBuildTaskHost process acting as a child node.
        /// </summary>
        internal bool CreateNode(TaskHostContext hostContext, INodePacketFactory factory, INodePacketHandler handler, TaskHostConfiguration configuration)
        {
            ErrorUtilities.VerifyThrowArgumentNull(factory, "factory");
            ErrorUtilities.VerifyThrow(!_nodeIdToPacketFactory.ContainsKey((int)hostContext), "We should not already have a factory for this context!  Did we forget to call DisconnectFromHost somewhere?");

            if (AvailableNodes == 0)
            {
                ErrorUtilities.ThrowInternalError("All allowable nodes already created ({0}).", _nodeContexts.Count);
                return false;
            }

            // Start the new process.  We pass in a node mode with a node number of 2, to indicate that we 
            // want to start up an MSBuild task host node. 
            string commandLineArgs = $" /nologo /nodemode:2 /nodereuse:{ComponentHost.BuildParameters.EnableNodeReuse} ";

            string msbuildLocation = GetMSBuildLocationFromHostContext(hostContext);

            // we couldn't even figure out the location we're trying to launch ... just go ahead and fail.  
            if (msbuildLocation == null)
            {
                return false;
            }

            CommunicationsUtilities.Trace("For a host context of {0}, spawning executable from {1}.", hostContext.ToString(), msbuildLocation ?? "MSBuild.exe");

            // Make it here.
            NodeContext context = GetNode
                                    (
                                        msbuildLocation,
                                        commandLineArgs,
                                        (int)hostContext,
                                        this,
                                        CommunicationsUtilities.GetTaskHostHostHandshake(hostContext),
                                        CommunicationsUtilities.GetTaskHostClientHandshake(hostContext),
                                        NodeContextTerminated
                                    );

            if (null != context)
            {
                _nodeContexts[hostContext] = context;

                // Start the asynchronous read.
                context.BeginAsyncPacketRead();

                _activeNodes.Add((int)hostContext);
                _noNodesActiveEvent.Reset();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Method called when a context terminates.
        /// </summary>
        private void NodeContextTerminated(int nodeId)
        {
            lock (_nodeContexts)
            {
                _nodeContexts.Remove((TaskHostContext)nodeId);
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
    }
}
