// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using System.Threading;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is resposible for managing the node providers - starting, stopping and sharing them.
    /// Although child nodes have a NodeManager, theirs do nothing as they never has any NodeProviders registered with them.
    /// </summary>
    internal class NodeManager
    {
        #region Constructors
        /// <summary>
        /// Default constructor.
        /// </summary>
        internal NodeManager(int cpuCount, bool childMode, Engine parentEngine)
        {
            nodeList = new List<ProvidersNodeInformation>();
            nodeProviders = new List<INodeProvider>();

            this.parentEngine = parentEngine;

            this.statusMessageReceived = new ManualResetEvent(false);

            // Create the inproc node, this means that there will always be one node, node 0
            if (taskExecutionModule == null)
            {
                taskExecutionModule = new TaskExecutionModule(parentEngine.EngineCallback,
                    cpuCount == 1 && !childMode ? TaskExecutionModule.TaskExecutionModuleMode.SingleProcMode :
                                                   TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, parentEngine.ProfileBuild);
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Register an instantiated INodeProvider with the node manager. The node manager will query the nodeprovider
        /// for a list of its node descriptions, and add these nodes to a master list of nodes which can be used
        /// by the scheduler. QUESTION: Do we allow duplicate Node Providers?
        /// </summary>
        /// <param name="providerToRegister"></param>
        /// <returns></returns>
        internal bool RegisterNodeProvider(INodeProvider nodeProviderToRegister)
        {
            ErrorUtilities.VerifyThrowArgumentNull(nodeProviderToRegister, nameof(nodeProviderToRegister));

            INodeDescription[] nodeDescriptions = nodeProviderToRegister.QueryNodeDescriptions();

            int[] nodeIds = new int[nodeDescriptions.Length];
            for (int i = 0; i < nodeIds.Length; i++)
            {
                nodeIds[i] = parentEngine.GetNextNodeId();
            }
            nodeProviderToRegister.AssignNodeIdentifiers(nodeIds);

            // Go through all of the nodes as described by nodeDescriptions and add them to out list of nodes
            for(int i=0; i < nodeDescriptions.Length;i++)
            {
                ProvidersNodeInformation nodeToAddFromProvider = 
                    new ProvidersNodeInformation(i, nodeIds[i], nodeDescriptions[i], nodeProviderToRegister);
                nodeList.Add(nodeToAddFromProvider);
            }

            nodeProviders.Add(nodeProviderToRegister);

            return true;
        }

        /// <summary>
        /// Provide an array of INodeDescriptionsof the node provided by the node provider for the node. The index of the description
        /// is the node index to which the description matches
        /// </summary>
        /// <returns></returns>
        internal INodeDescription[] GetNodeDescriptions()
        {
          // The number of node descriptions is the number of nodes from all of the node providers, plus one for the default "0" node
          INodeDescription[] nodeDescription = new INodeDescription[nodeList.Count+1];
          nodeDescription[0] = null;
          for (int nodeListIndex = 0; nodeListIndex < nodeList.Count; nodeListIndex++)
          {
              ProvidersNodeInformation nodeInfo = nodeList[nodeListIndex];
              // +1 because the node description already has the 0 element set to null
              nodeDescription[nodeListIndex + 1] = nodeInfo.Description;
          }
          return nodeDescription;
        }

        /// <summary>
        /// Register node logger with all currently available providers
        /// </summary>
        /// <param name="loggerDescription"></param>
        internal void RegisterNodeLogger(LoggerDescription loggerDescription)
        {
            foreach (INodeProvider nodeProvider in nodeProviders)
            {
                nodeProvider.RegisterNodeLogger(loggerDescription);
            }
        }

        /// <summary>
        /// Request status from all nodes in the system
        /// </summary>
        /// <param name="responseTimeout"></param>
        /// <returns></returns>
        internal NodeStatus[] RequestStatusForNodes(int responseTimeout)
        {
            int requestId = 0;

            statusForNodes = new NodeStatus[nodeList.Count];
            statusReplyCount = 0;
            statusMessageReceived.Reset();

            // Request status from all registered nodes
            for (int i = 0; i < nodeList.Count; i++)
            {
                nodeList[i].NodeProvider.RequestNodeStatus(nodeList[i].NodeIndex, requestId);
            }

            long startTime = DateTime.Now.Ticks;

            while (statusReplyCount < nodeList.Count)
            {
                if (statusMessageReceived.WaitOne(responseTimeout, false))
                {
                    // We received another reply
                    statusMessageReceived.Reset();
                    // Calculate the time remaining and only continue if there is time left
                    TimeSpan timeSpent = new TimeSpan(DateTime.Now.Ticks - startTime);
                    startTime = DateTime.Now.Ticks;
                    responseTimeout -= (int)timeSpent.TotalMilliseconds;
                    if (responseTimeout <= 0)
                    {
                        Console.WriteLine("Response time out out exceeded :" + DateTime.Now.Ticks);
                        break;
                    }
                }
                else
                {
                    // Timed out waiting for the response from the node
                    Console.WriteLine("Response time out out exceeded:" + DateTime.Now.Ticks);
                    break;
                }
            }

            return statusForNodes;
        }

        internal void PostNodeStatus(int nodeId, NodeStatus nodeStatus)
        {
            ErrorUtilities.VerifyThrow( nodeStatus.RequestId != NodeStatus.UnrequestedStatus,
                                        "Node manager should not receive unrequested status");

            NodeStatus[] currentStatus = statusForNodes;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].NodeId == nodeId)
                {
                    currentStatus[i] = nodeStatus;
                    break;
                }
            }

            statusReplyCount++;
            statusMessageReceived.Set();
        }

        internal void PostCycleNotification
        (
            int nodeId, 
            TargetInProgessState child, 
            TargetInProgessState parent)
        {
            if (nodeId == 0)
            {
                parentEngine.Introspector.BreakCycle(child, parent);
            }

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].NodeId == nodeId)
                {
                    nodeList[i].NodeProvider.PostIntrospectorCommand(nodeList[i].NodeIndex, child, parent);
                    break;
                }
            }
        }

        /// <summary>
        /// Shut down each of the nodes for all providers registered to the node manager.
        /// Shuts down the TEM.
        /// </summary>
        internal void ShutdownNodes(Node.NodeShutdownLevel nodeShutdownLevel)
        {
            foreach (INodeProvider nodeProvider in nodeProviders)
            {
                nodeProvider.ShutdownNodes(nodeShutdownLevel);
            }

            // Don't shutdown the TEM if the engine maybe reused for another build
            if (nodeShutdownLevel != Node.NodeShutdownLevel.BuildCompleteFailure &&
                nodeShutdownLevel != Node.NodeShutdownLevel.BuildCompleteSuccess)
            {
                if (taskExecutionModule != null)
                {
                    taskExecutionModule.Shutdown();
                    taskExecutionModule = null;
                    // At this point we have nulled out the task execution module and have told our task worker threads to exit
                    // we do not want the engine build loop to continue to do any work becasue the operations of the build loop
                    // require the task execution module in many cases. Before this fix, when the engine build loop was allowed
                    // to do work after the task execution module we would get random null reference excetpions depending on 
                    // what was the first line to use the TEM after it was nulled out.
                    parentEngine.SetEngineAbortTo(true);
                }
            }
        }

        /// <summary>
        /// Post a build result to a node, the node index is an index into the list of nodes provided by all node providers
        /// registered to the node manager, the 0 in index is a local call to taskexecutionmodule
        /// </summary>
        /// <param name="nodeIndex"></param>
        /// <param name="buildResult"></param>
        internal void PostBuildResultToNode(int nodeIndex, BuildResult buildResult)
        {
            ErrorUtilities.VerifyThrow(nodeIndex <= nodeList.Count, "Should not pass a node index higher then the number of nodes in nodeManager");
            if (nodeIndex != 0)
            {
                nodeList[nodeIndex-1].NodeProvider.PostBuildResultToNode(nodeList[nodeIndex-1].NodeIndex, buildResult);
            }
            else
            {
                taskExecutionModule.PostBuildResults(buildResult);
            }
        }
        /// <summary>
        /// Post a build request to a node, the node index is an index into the list of nodes provided by all node providers
        /// registered to the node manager, the 0 in index is a local call to taskexecutionmodule
        /// </summary>
        /// <param name="nodeIndex"></param>
        /// <param name="buildRequest"></param>
        internal void PostBuildRequestToNode(int nodeIndex, BuildRequest buildRequest)
        {
            ErrorUtilities.VerifyThrow(nodeIndex != 0, "Should not use NodeManager to post to local TEM");
            nodeList[nodeIndex - 1].NodeProvider.PostBuildRequestToNode(nodeList[nodeIndex - 1].NodeIndex, buildRequest);
        }

        /// <summary>
        /// Execute a task on the local node
        /// </summary>
        /// <param name="taskState"></param>
        internal void ExecuteTask(TaskExecutionState taskState)
        {
            taskExecutionModule.ExecuteTask(taskState);
        }

        /// <summary>
        /// TEMPORARY
        /// </summary>
        internal void UpdateSettings
        (
            bool enableOutofProcLogging, 
            bool enableOnlyLogCriticalEvents,
            bool useBreadthFirstTraversal
        )
        {
            foreach (INodeProvider nodeProvider in nodeProviders)
            {
                nodeProvider.UpdateSettings(enableOutofProcLogging, enableOnlyLogCriticalEvents, useBreadthFirstTraversal);
            }
        }

        internal void ChangeNodeTraversalType(bool breadthFirstTraversal)
        {
            UpdateSettings(parentEngine.EnabledCentralLogging, parentEngine.OnlyLogCriticalEvents, breadthFirstTraversal);
        }

        #endregion

        #region Properties
        
        /// <summary>
        /// Getter access to the local node
        /// </summary>
        internal  TaskExecutionModule TaskExecutionModule
        {
            get
            {
                return taskExecutionModule;
            }
            set
            {
                taskExecutionModule = value;
            }
        }
        
        /// <summary>
        /// Number of Nodes being managed by NodeManager
        /// </summary>
        internal int MaxNodeCount
        {
            get
            {
                // add 1 for the local node (taskExecutionModule)
                return nodeList.Count+1;
            }
        }
        #endregion

        #region Data
        /// <summary>
        /// Pointer to the parent engine
        /// </summary>
        private Engine parentEngine;
        /// <summary>
        /// List of node information of nodes provided by registered node providers
        /// </summary>
        private List<ProvidersNodeInformation> nodeList;
        /// <summary>
        /// List of registered node providers
        /// </summary>
        private List<INodeProvider> nodeProviders;
        /// <summary>
        /// Array of status summaries from the node
        /// </summary>
        private NodeStatus[] statusForNodes;
        /// <summary>
        /// Count of status replies recieved
        /// </summary>
        private int statusReplyCount;
        /// <summary>
        /// An event activated when status message arrives
        /// </summary>
        private ManualResetEvent statusMessageReceived;
        /// <summary>
        /// Local TEM used for executing tasks within the current process
        /// </summary>
        private TaskExecutionModule taskExecutionModule;
        #endregion
    }

    /// <summary>
    /// Class which contains, information about each of the nodes provided by each of the node providers registered to node manager
    /// </summary>
    internal class ProvidersNodeInformation
    {
        #region Constructors
        internal ProvidersNodeInformation
        (
            int nodeProviderNodeIndex,
            int nodeId,
            INodeDescription nodeProviderDescription,
            INodeProvider nodeProviderReference
        )
        {
            this.nodeIndex = nodeProviderNodeIndex;
            this.nodeId = nodeId;
            this.description = nodeProviderDescription;
            this.nodeProvider = nodeProviderReference;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Node provider for node
        /// </summary>
        internal INodeProvider NodeProvider
        {
            get { return nodeProvider; }
        }

        /// <summary>
        /// Node description for node
        /// </summary>
        internal INodeDescription Description
        {
            get
            {
                return description;
            }
        }

        /// <summary>
        /// Node index relative to the node provider to which it is attached
        /// </summary>
        internal int NodeIndex
        {
            get
            {
                return nodeIndex;
            }
        }

        /// <summary>
        /// The nodeId issued by the engine to this node
        /// </summary>
        internal int NodeId
        {
            get
            {
                return nodeId;
            }
        }
        #endregion

        #region Data
        // Index from nodeProvider of a node which it manages
        private int nodeIndex;

        // Node description of node in nodeProvider referenced by nodeIndex;
        private INodeDescription description;

        // Reference to the nodeProvider which manages the node referenced by nodeIndex
        private INodeProvider nodeProvider;

        // The nodeId issued by the engine to this node
        private int nodeId;
        #endregion
    }
}
