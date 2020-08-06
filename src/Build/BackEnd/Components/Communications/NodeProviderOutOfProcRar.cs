using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using System.Collections.Generic;

namespace Microsoft.Build.BackEnd
{
    internal sealed class NodeProviderOutOfProcRar : NodeProviderOutOfProcBase, INodeProvider
    {
        private int? _rarNodeId = null;
        private NodeContext _rarNodeContext = null;

        public NodeProviderType ProviderType => NodeProviderType.ResolveAssemblyReference;

        public int AvailableNodes => 1;

        public bool CreateNode(int nodeId, INodePacketFactory factory, NodeConfiguration configuration)
        {
            ErrorUtilities.VerifyThrowArgumentNull(factory, "factory");
            ErrorUtilities.VerifyThrow(_rarNodeId.HasValue, "RAR node already created.");

            // Start the new process.  We pass in a node mode with a node number of 3, to indicate that we
            // want to start up RAR out-of-proc node.
            // Note: We need to always pass /nodeReuse to ensure the value for /nodeReuse from msbuild.rsp
            // (next to msbuild.exe) is ignored.
            string commandLineArgs = $"/nologo /nodemode:3 /nodeReuse:{ComponentHost.BuildParameters.EnableNodeReuse.ToString().ToLower()} /low:{ComponentHost.BuildParameters.LowPriority.ToString().ToLower()}";

            // Make it here.
            CommunicationsUtilities.Trace("Starting to acquire a new or existing node to establish node ID {0}...", nodeId);

            Handshake hostHandshake = new Handshake(CommunicationsUtilities.GetHandshakeOptions(taskHost: false,
                                                            nodeReuse: ComponentHost.BuildParameters.EnableNodeReuse,
                                                            lowPriority: ComponentHost.BuildParameters.LowPriority,
                                                            is64Bit: EnvironmentUtilities.Is64BitProcess,
                                                            workerNode: false));
            NodeContext context = GetNode(null, commandLineArgs, nodeId, factory, hostHandshake, NodeContextTerminated);

            if (context != null)
            {
                _rarNodeId = nodeId;
                _rarNodeContext = context;

                // Is this necesarry?
                // Start the asynchronous read.
                context.BeginAsyncPacketRead();

                // Configure the node.
                context.SendData(configuration);

                return true;
            }

            throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotConnectToMSBuildExe", ComponentHost.BuildParameters.NodeExeLocation));
        }

        private void NodeContextTerminated(int nodeId)
        {
            if (_rarNodeId == nodeId)
            {
                _rarNodeId = null;
                _rarNodeContext = null;
            }
        }

        #region IBuildComponent Members

        /// <summary>
        /// Initializes the component.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            this.ComponentHost = host;
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
        static internal IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            ErrorUtilities.VerifyThrow(componentType == BuildComponentType.OutOfProcNodeRarProvider, "Factory cannot create components of type {0}", componentType);
            return new NodeProviderOutOfProcRar();
        }

        public void SendData(int node, INodePacket packet)
        {
            ErrorUtilities.VerifyThrowInternalNull(packet, "Packet is null");

            if (_rarNodeId == node)
            {
                _rarNodeContext.SendData(packet);
            }
        }

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

            // RAR node is special...
            if (!nodeReuse)
                ShutdownAllNodes(nodeReuse, NodeContextTerminated);
        }

        public void ShutdownConnectedNodes(bool enableReuse)
        {
            // RAR node is special
            if (_rarNodeId != null && !enableReuse)
            {
                List<NodeContext> contextList = new List<NodeContext>()
                {
                    _rarNodeContext
                };
                ShutdownConnectedNodes(contextList, enableReuse);
            }
        }
    }
}
