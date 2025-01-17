// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            CommunicationsUtilities.Trace("MSBUILDNODEHANDSHAKESALT=\"{0}\", msbuildDirectory=\"{1}\", enableNodeReuse={2}, enableLowPriority={3}", Traits.MSBuildNodeHandshakeSalt, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32, enableNodeReuse, enableLowPriority);
            return new Handshake(CommunicationsUtilities.GetHandshakeOptions(taskHost: false, architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture(), nodeReuse: enableNodeReuse, lowPriority: enableLowPriority));
        }

        /// <summary>
        /// Instantiates a new MSBuild processes acting as a child nodes or connect to existing ones.
        /// </summary>
        public IList<NodeInfo> CreateNodes(int nextNodeId, INodePacketFactory factory, Func<NodeInfo, NodeConfiguration> configurationFactory, int numberOfNodesToCreate)
        {
            ErrorUtilities.VerifyThrowArgumentNull(factory);

            // This can run concurrently. To be properly detect internal bug when we create more nodes than allowed
            //   we add into _nodeContexts premise of future node and verify that it will not cross limits.
            if (_nodeContexts.Count + numberOfNodesToCreate > ComponentHost.BuildParameters.MaxNodeCount)
            {
                ErrorUtilities.ThrowInternalError("Exceeded max node count of '{0}', current count is '{1}' ", ComponentHost.BuildParameters.MaxNodeCount, _nodeContexts.Count);
                return new List<NodeInfo>();
            }

            ConcurrentBag<NodeInfo> nodes = new();

            // Start the new process.  We pass in a node mode with a node number of 1, to indicate that we
            // want to start up just a standard MSBuild out-of-proc node.
            // Note: We need to always pass /nodeReuse to ensure the value for /nodeReuse from msbuild.rsp
            // (next to msbuild.exe) is ignored.
            string commandLineArgs = $"/noautoresponse /nologo /nodemode:1 /nodeReuse:{ComponentHost.BuildParameters.EnableNodeReuse.ToString().ToLower()} /low:{ComponentHost.BuildParameters.LowPriority.ToString().ToLower()}";

            CommunicationsUtilities.Trace("Starting to acquire {1} new or existing node(s) to establish nodes from ID {0} to {2}...", nextNodeId, numberOfNodesToCreate, nextNodeId + numberOfNodesToCreate - 1);

            Handshake hostHandshake = new(CommunicationsUtilities.GetHandshakeOptions(taskHost: false, architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture(), nodeReuse: ComponentHost.BuildParameters.EnableNodeReuse, lowPriority: ComponentHost.BuildParameters.LowPriority));
            IList<NodeContext> nodeContexts = GetNodes(null, commandLineArgs, nextNodeId, factory, hostHandshake, NodeContextCreated, NodeContextTerminated, numberOfNodesToCreate);

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
            ErrorUtilities.VerifyThrow(_nodeContexts.ContainsKey(nodeId), "Invalid node id specified: {0}.", nodeId);

            SendData(_nodeContexts[nodeId], packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            // Send the build completion message to the nodes, causing them to shutdown or reset.
            var contextsToShutDown = new List<NodeContext>(_nodeContexts.Values);

            ShutdownConnectedNodes(contextsToShutDown, enableReuse);
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
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
        }

        /// <summary>
        /// Shuts down the component
        /// </summary>
        public void ShutdownComponent()
        {
        }

        #endregion

        /// <summary>
        /// Static factory for component creation.
        /// </summary>
        internal static IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            ErrorUtilities.VerifyThrow(componentType == BuildComponentType.OutOfProcNodeProvider, "Factory cannot create components of type {0}", componentType);
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
            return _nodeContexts.Values.Select(context => context.Process);
        }
    }
}
