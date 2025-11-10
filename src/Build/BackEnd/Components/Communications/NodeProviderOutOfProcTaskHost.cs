// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

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
        /// </summary>
        private ConcurrentDictionary<int, NodeContext> _nodeContexts;

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
                throw new NotImplementedException("This property is not implemented because available nodes are unlimited.");
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
            ErrorUtilities.VerifyThrow(_nodeContexts.TryGetValue(nodeId, out NodeContext context), "Invalid host context specified: {0}.", nodeId);

            SendData(context, packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            // Send the build completion message to the nodes, causing them to shutdown or reset.
            List<NodeContext> contextsToShutDown = [.. _nodeContexts.Values];

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
            _nodeContexts = new ConcurrentDictionary<int, NodeContext>();
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
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            if (_nodeIdToPacketFactory.TryGetValue(nodeId, out INodePacketFactory nodePacketFactory))
            {
                nodePacketFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
            }
            else
            {
                _localPacketFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
            }
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
            ErrorUtilities.VerifyThrowInternalErrorUnreachable(Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.TaskHost));
            if (Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.CLR2))
            {
                return TaskHostNameForClr2TaskHost;
            }

            if (string.IsNullOrEmpty(s_msbuildName))
            {
                s_msbuildName = Environment.GetEnvironmentVariable("MSBUILD_EXE_NAME");
                if (!string.IsNullOrEmpty(s_msbuildName))
                {
                    return s_msbuildName;
                }

#if NETFRAMEWORK
                // In .NET Framework, use dotnet for .NET task hosts
                if (Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.NET))
                {
                    s_msbuildName = Constants.DotnetProcessName;

                    return s_msbuildName;
                }
#endif
                // Default based on whether it's .NET or Framework
                s_msbuildName = Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.NET)
                    ? Constants.MSBuildAssemblyName
                    : Constants.MSBuildExecutableName;
            }

            return s_msbuildName;
        }

        /// <summary>
        /// Given a TaskHostContext, returns the appropriate runtime host and MSBuild assembly locations
        /// based on the handshake options.
        /// </summary>
        /// <param name="hostContext">The handshake options specifying the desired task host configuration (architecture, CLR version, runtime).</param>
        /// <returns>
        /// The full path to MSBuild.exe.
        /// </returns>
        internal static string GetMSBuildExecutablePathForNonNETRuntimes(HandshakeOptions hostContext)
        {
            ErrorUtilities.VerifyThrowInternalErrorUnreachable(Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.TaskHost));

            var toolName = GetTaskHostNameFromHostContext(hostContext);
            s_baseTaskHostPath = BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32;
            s_baseTaskHostPath64 = BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64;
            s_baseTaskHostPathArm64 = BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryArm64;

            bool isX64 = Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.X64);
            bool isArm64 = Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.Arm64);
            bool isCLR2 = Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.CLR2);

            // Unsupported combinations
            if (isArm64 && isCLR2)
            {
                ErrorUtilities.ThrowInternalError("ARM64 CLR2 task hosts are not supported.");
            }

            if (isCLR2)
            {
                return isX64 ? Path.Combine(GetOrInitializeX64Clr2Path(toolName), toolName) : Path.Combine(GetOrInitializeX32Clr2Path(toolName), toolName);
            }

            if (isX64)
            {
                return Path.Combine(s_pathToX64Clr4 ??= s_baseTaskHostPath64, toolName);
            }

            if (isArm64)
            {
                return Path.Combine(s_pathToArm64Clr4 ??= s_baseTaskHostPathArm64, toolName);
            }

            return Path.Combine(s_pathToX32Clr4 ??= s_baseTaskHostPath, toolName);
        }

        /// <summary>
        /// Handles the handshake scenario where a .NET task host is requested from a .NET Framework process.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// - RuntimeHostPath: The path to the dotnet executable that will host the .NET runtime
        /// - MSBuildAssemblyPath: The full path to MSBuild.dll that will be loaded by the dotnet host.
        /// </returns>
        internal static (string RuntimeHostPath, string MSBuildAssemblyPath) GetMSBuildLocationForNETRuntime(HandshakeOptions hostContext, TaskHostParameters taskHostParameters)
        {
            ErrorUtilities.VerifyThrowInternalErrorUnreachable(Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.TaskHost));

            return (taskHostParameters.DotnetHostPath, GetMSBuildAssemblyPath(taskHostParameters));
        }

        private static string GetMSBuildAssemblyPath(in TaskHostParameters taskHostParameters)
        {
            if (taskHostParameters.MSBuildAssemblyPath != null)
            {
                ValidateNetHostSdkVersion(taskHostParameters.MSBuildAssemblyPath);

                return taskHostParameters.MSBuildAssemblyPath;
            }

            throw new InvalidProjectFileException(ResourceUtilities.GetResourceString("NETHostTaskLoad_Failed"));

            static void ValidateNetHostSdkVersion(string path)
            {
                const int MinimumSdkVersion = 10;

                if (string.IsNullOrEmpty(path))
                {
                    ErrorUtilities.ThrowInternalError(ResourceUtilities.GetResourceString("SDKPathResolution_Failed"));
                }

                if (!FileSystems.Default.DirectoryExists(path))
                {
                    ErrorUtilities.ThrowInternalError(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("SDKPathCheck_Failed", path));
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

        private static string GetOrInitializeX64Clr2Path(string toolName)
        {
            s_pathToX64Clr2 ??= GetPathFromEnvironmentOrDefault("MSBUILDTASKHOSTLOCATION64", s_baseTaskHostPath64, toolName);

            return s_pathToX64Clr2;
        }

        private static string GetOrInitializeX32Clr2Path(string toolName)
        {
            s_pathToX32Clr2 ??= GetPathFromEnvironmentOrDefault("MSBUILDTASKHOSTLOCATION", s_baseTaskHostPath, toolName);

            return s_pathToX32Clr2;
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
        /// Make sure a node in the requested context exists.
        /// </summary>
        internal bool AcquireAndSetUpHost(
            HandshakeOptions hostContext,
            int taskHostNodeId,
            INodePacketFactory factory,
            INodePacketHandler handler,
            TaskHostConfiguration configuration,
            in TaskHostParameters taskHostParameters)
        {
            bool nodeCreationSucceeded;
            if (!_nodeContexts.ContainsKey(taskHostNodeId))
            {
                nodeCreationSucceeded = CreateNode(hostContext, taskHostNodeId, factory, handler, configuration, taskHostParameters);
            }
            else
            {
                // node already exists, so "creation" automatically succeeded
                nodeCreationSucceeded = true;
            }

            if (nodeCreationSucceeded)
            {
                NodeContext context = _nodeContexts[taskHostNodeId];
                _nodeIdToPacketFactory[taskHostNodeId] = factory;
                _nodeIdToPacketHandler[taskHostNodeId] = handler;

                // Configure the node.
                context.SendData(configuration);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Expected to be called when TaskHostTask is done with host of the given context.
        /// </summary>
        internal void DisconnectFromHost(int nodeId)
        {
            ErrorUtilities.VerifyThrow(_nodeIdToPacketFactory.ContainsKey(nodeId) && _nodeIdToPacketHandler.ContainsKey(nodeId), "Why are we trying to disconnect from a context that we already disconnected from?  Did we call DisconnectFromHost twice?");

            _nodeIdToPacketFactory.Remove(nodeId);
            _nodeIdToPacketHandler.Remove(nodeId);
        }

        /// <summary>
        /// Instantiates a new MSBuild or MSBuildTaskHost process acting as a child node.
        /// </summary>
        internal bool CreateNode(HandshakeOptions hostContext, int taskHostNodeId, INodePacketFactory factory, INodePacketHandler handler, TaskHostConfiguration configuration, in TaskHostParameters taskHostParameters)
        {
            ErrorUtilities.VerifyThrowArgumentNull(factory);
            ErrorUtilities.VerifyThrow(!_nodeIdToPacketFactory.ContainsKey(taskHostNodeId), "We should not already have a factory for this context!  Did we forget to call DisconnectFromHost somewhere?");

            // If runtime host path is null it means we don't have MSBuild.dll path resolved and there is no need to include it in the command line arguments.
            string commandLineArgsPlaceholder = "\"{0}\" /nologo /nodemode:2 /nodereuse:{1} /low:{2} ";

            IList<NodeContext> nodeContexts;

            // Handle .NET task host context
#if NETFRAMEWORK
            if (Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.NET))
            {
                (string runtimeHostPath, string msbuildAssemblyPath) = GetMSBuildLocationForNETRuntime(hostContext, taskHostParameters);

                CommunicationsUtilities.Trace("For a host context of {0}, spawning dotnet.exe from {1}.", hostContext.ToString(), runtimeHostPath);

                var handshake = new Handshake(hostContext, predefinedToolsDirectory: msbuildAssemblyPath);

                // There is always one task host per host context so we always create just 1 one task host node here.      
                nodeContexts = GetNodes(
                    runtimeHostPath,
                    string.Format(commandLineArgsPlaceholder, Path.Combine(msbuildAssemblyPath, Constants.MSBuildAssemblyName), NodeReuseIsEnabled(hostContext), ComponentHost.BuildParameters.LowPriority),
                    taskHostNodeId,
                    this,
                    handshake,
                    NodeContextCreated,
                    NodeContextTerminated,
                    1);

                return nodeContexts.Count == 1;
            }
#endif

            string msbuildLocation = GetMSBuildExecutablePathForNonNETRuntimes(hostContext);

            // we couldn't even figure out the location we're trying to launch ... just go ahead and fail.
            if (msbuildLocation == null)
            {
                return false;
            }

            CommunicationsUtilities.Trace("For a host context of {0}, spawning executable from {1}.", hostContext.ToString(), msbuildLocation ?? Constants.MSBuildExecutableName);

            nodeContexts = GetNodes(
                msbuildLocation,
                string.Format(commandLineArgsPlaceholder, string.Empty, NodeReuseIsEnabled(hostContext), ComponentHost.BuildParameters.LowPriority),
                taskHostNodeId,
                this,
                new Handshake(hostContext),
                NodeContextCreated,
                NodeContextTerminated,
                1);

            return nodeContexts.Count == 1;

            // Determines whether node reuse should be enabled for the given host context.
            // Node reuse allows MSBuild to reuse existing task host processes for better performance,
            // but is disabled for CLR2 because it uses legacy MSBuildTaskHost.
            bool NodeReuseIsEnabled(HandshakeOptions hostContext)
            {
                bool isCLR2 = Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.CLR2);

                return Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.NodeReuse)
                    && !isCLR2;
            }
        }

        /// <summary>
        /// Method called when a context created.
        /// </summary>
        private void NodeContextCreated(NodeContext context)
        {
            _nodeContexts[context.NodeId] = context;

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
            _nodeContexts.TryRemove(nodeId, out _);

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

        public IEnumerable<Process> GetProcesses() => _nodeContexts.Values.Select(context => context.Process);
    }
}
