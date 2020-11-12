// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This interface is used by to provide an engine coordinating a build with access
    /// to child engines which can execute parts of the build. The provider is entirely 
    /// responsible for establishing and maintaining the communication between the parent
    /// engine and the child engines. The provider is also responsible for describing the 
    /// capabilities of the communication channel and the machines on which the child engines 
    /// are running so that the parent engine can schedule and coordinate the work appropriately
    /// </summary>
    internal interface INodeProvider
    {
        /// <summary>
        /// This method is called by the NodeManager at the engine start up to initialize
        /// each provider. The configuration string is passed verbatim from the command line
        /// to the node provider.
        /// </summary>
        /// <param name="configuration">Configuration string</param>
        /// <param name="engineCallback">Interface to use to make engine callbacks</param>
        /// <param name="parentGlobalProperties">Properties to be passed to the child engine</param>
        /// <param name="toolsetSearchLocations">Locations to search to toolset paths</param>
        /// <param name="startupDirectory">Directory from which the parent msbuild.exe was originally invoked</param>
        void Initialize(string configuration, IEngineCallback engineCallback, BuildPropertyGroup parentGlobalProperties,
                        ToolsetDefinitionLocations toolsetSearchLocations, string startupDirectory);

        /// <summary>
        /// This method is called by the NodeManager after the Initialize method to query 
        /// the provider about number and capability of the nodes that it can make available to
        /// the parent engine. 
        /// </summary>
        /// <returns>Description of nodes that this provider</returns>
        INodeDescription[] QueryNodeDescriptions();

        /// <summary>
        /// This method is called by the NodeManager after it queries the provider via QueryNodeDescription
        /// to provider a unique identifier for each node exposed by the provider. This method can only be called
        /// after Initialize method has been called.
        /// </summary>
        /// <param name="nodeIdentifiers">An array of integer tokens which identify each node</param>
        void AssignNodeIdentifiers(int[] nodeIdentifiers);

        /// <summary>
        /// This method is called by the NodeManager to pass in a description of a forwarding logger 
        /// that should be loaded on the nodes exposed by the provider. This method can only be called
        /// after Initialize method has been called.
        /// </summary>
        /// <param name="loggerDescription"></param>
        void RegisterNodeLogger(LoggerDescription loggerDescription);

        /// <summary>
        /// This method is called by the scheduler to request one of the nodes exposed by
        /// this node provider to build a certain part of the tree. The node is expected to
        /// pass back buildResult once the build is completed on the remote node
        /// </summary>
        /// <param name="nodeIndex">The token indicating which node to use</param>
        /// <param name="buildRequest">Description of the build request</param>
        void PostBuildRequestToNode(int nodeIndex, BuildRequest buildRequest);

        /// <summary>
        /// This method is called by the coordinating engine to send results requested by a 
        /// node during intermediate evaluation
        /// </summary>
        /// <param name="nodeIndex"></param>
        /// <param name="buildRequest"></param>
        void PostBuildResultToNode(int nodeIndex, BuildResult buildResult);

        /// <summary>
        /// This method is called by the coordinating engine to request the current status of the node.
        /// This method is used as both a "ping" and to measure the load on the node.
        /// </summary>
        void RequestNodeStatus(int nodeIndex, int requestId);

        /// <summary>
        /// This method is called by the NodeManager when the parent engine indicates that is no
        /// longer needs the node (typically this is done when the parent engine is shutting down)
        /// </summary>
        void ShutdownNodes(Node.NodeShutdownLevel nodeShutdownLevel);

        /// <summary>
        /// Tell the nodes to use central logging, UNDONE
        /// </summary>
        void UpdateSettings(bool enableCentralizedLogging, bool enableOnlyLogCriticalEvents, bool useBreadFirstTraversal);

        void PostIntrospectorCommand(int nodeIndex, TargetInProgessState child, TargetInProgessState parent);
    }
}
