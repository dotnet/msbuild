// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is responsible for determining on which node and when a task should be executed. It
    /// receives work requests from the Target class and communicates to the appropriate node.
    /// </summary>
    internal class Scheduler
    {
        #region Constructors
        /// <summary>
        /// Create the scheduler.
        /// </summary>
        /// <param name="nodeId">the id of the node where the scheduler was instantiated on</param>
        /// <param name="parentEngine">a reference to the engine who instantiated the scheduler</param>
        internal Scheduler(int nodeId, Engine parentEngine)
        {
            this.localNodeId = nodeId;
            this.childMode = true;
            this.parentEngine = parentEngine;
        }

        #endregion

        #region Properties

        #endregion

        #region Methods
        /// <summary>
        /// Provide the scheduler with the information about the available nodes. This function has to be
        /// called after the NodeManager has initialzed all the node providers
        /// </summary>
        internal void Initialize(INodeDescription[] nodeDescriptions)
        {
            this.nodes = nodeDescriptions;
            this.childMode = false;

            this.handleIdToScheduleRecord = new Dictionary<ScheduleRecordKey, ScheduleRecord>();
            this.scheduleTableLock = new object();

            this.totalRequestsPerNode = new int[nodes.Length];
            this.blockedRequestsPerNode = new int[nodes.Length];
            this.postBlockCount = new int[nodes.Length];

            for (int i = 0; i < totalRequestsPerNode.Length; i++)
            {
                totalRequestsPerNode[i] = 0;
                blockedRequestsPerNode[i] = 0;
                postBlockCount[i] = 0;
            }

            this.useLoadBalancing = (Environment.GetEnvironmentVariable("MSBUILDLOADBALANCE") != "0");
            this.lastUsedNode = 0;
        }

        /// <summary>
        /// This method specifies which node a particular build request has to be evaluated on.
        /// </summary>>
        /// <returns>Id of the node on which the build request should be performed</returns>
        internal int CalculateNodeForBuildRequest(BuildRequest currentRequest, int nodeIndexCurrent)
        {
            int nodeUsed = EngineCallback.inProcNode;
            if (childMode)
            {
                // If the project is already loaded on the current node or if the request
                // was sent from the parent - evaluate the request locally. In all other
                // cases send the request over to the parent
                if (nodeIndexCurrent != localNodeId && !currentRequest.IsExternalRequest)
                {
                    // This is the same as using EngineCallback.parentNode
                    nodeUsed = -1;
                }
            }
            else
            {
                // In single proc case return the current node
                if (nodes.Length == 1)
                {
                    return nodeUsed;
                }

                // If the project is not loaded either locally or on a remote node - calculate the best node to use
                // If there are nodes with less than "nodeWorkLoadProjectCount" projects in progress, choose the node
                // with the lowest in progress projects. Otherwise choose a node which has the least
                // number of projects loaded. Resolve a tie in number of projects loaded by looking at the number
                // of inprogress projects
                nodeUsed = nodeIndexCurrent;
                // If we have not chosen an node yet, this can happen if the node was loaded previously on a child node
                if (nodeUsed == EngineCallback.invalidNode)
                {
                    if (useLoadBalancing)
                    {
                        #region UseLoadBalancing
                        int blockedNode = EngineCallback.invalidNode;
                        int blockedNodeRemainingProjectCount = nodeWorkLoadProjectCount;
                        int leastBusyNode = EngineCallback.invalidNode;
                        int leastBusyInProgressCount = -1;
                        int leastLoadedNode = EngineCallback.inProcNode;
                        int leastLoadedLoadCount = totalRequestsPerNode[EngineCallback.inProcNode];
                        int leastLoadedBlockedCount = blockedRequestsPerNode[EngineCallback.inProcNode];

                        for (int i = 0; i < nodes.Length; i++)
                        {
                            //postBlockCount indicates the number of projects which should be sent to a node to unblock it due to the 
                            //node running out of work.
                            if (postBlockCount[i] != 0 && postBlockCount[i] < blockedNodeRemainingProjectCount)
                            {
                                blockedNode = i;
                                blockedNodeRemainingProjectCount = postBlockCount[i];
                            }
                            else
                            {
                                // Figure out which node has the least ammount of in progress work
                                int perNodeInProgress = totalRequestsPerNode[i] - blockedRequestsPerNode[i];
                                if ((perNodeInProgress < nodeWorkLoadProjectCount) &&
                                    (perNodeInProgress < leastBusyInProgressCount || leastBusyInProgressCount == -1))
                                {
                                    leastBusyNode = i;
                                    leastBusyInProgressCount = perNodeInProgress;
                                }
                                // Find the node with the least ammount of requests in total
                                // or if the number of requests are the same find the node with the
                                // node with the least number of blocked requests
                                if ((totalRequestsPerNode[i] < leastLoadedLoadCount) ||
                                    (totalRequestsPerNode[i] == leastLoadedLoadCount && blockedRequestsPerNode[i] < leastLoadedBlockedCount))
                                {
                                    leastLoadedNode = i;
                                    leastLoadedLoadCount = totalRequestsPerNode[i];
                                    leastLoadedBlockedCount = perNodeInProgress;
                                }
                            }
                        }

                        // Give the work to a node blocked due to having no work . If there are no nodes without work
                        // give the work to the least loaded node
                        if (blockedNode != EngineCallback.invalidNode)
                        {
                            nodeUsed = blockedNode;
                            postBlockCount[blockedNode]--;
                        }
                        else
                        {
                            nodeUsed = (leastBusyNode != EngineCallback.invalidNode) ? leastBusyNode : leastLoadedNode;
                        }
                        #endregion
                    }
                    else
                    {
                        // round robin schedule the build request 
                        nodeUsed = (lastUsedNode % nodes.Length);

                        // Running total of the number of times this round robin scheduler has been called
                        lastUsedNode++;

                        if (postBlockCount[nodeUsed] != 0)
                        {
                            postBlockCount[nodeUsed]--;
                        }
                    }
                }

                // Update the internal data structure to reflect the scheduling decision
                NotifyOfSchedulingDecision(currentRequest, nodeUsed);
            }
            return nodeUsed;
        }

        /// <summary>
        /// This method is called to update the datastructures to reflect that given request will
        /// be built on a given node.
        /// </summary>
        /// <param name="currentRequest"></param>
        /// <param name="nodeUsed"></param>
        internal void NotifyOfSchedulingDecision(BuildRequest currentRequest, int nodeUsed)
        {
            // Don't update structures on the child node or in single proc mode
            if (childMode || nodes.Length == 1)
            {
                return;
            }

            // Update the count of requests being build on the node
            totalRequestsPerNode[nodeUsed]++;

            // Ignore host requests
            if (currentRequest.HandleId == EngineCallback.invalidEngineHandle)
            {
                return;
            }

            if (Engine.debugMode)
            {
                string targetnames = currentRequest.TargetNames != null ? String.Join(";", currentRequest.TargetNames) : "null";
                Console.WriteLine("Sending project " + currentRequest.ProjectFileName + " Target " + targetnames + " to " + nodeUsed);
            }

            // Update the records
            ScheduleRecordKey recordKey = new ScheduleRecordKey(currentRequest.HandleId, currentRequest.RequestId);
            ScheduleRecordKey parentKey = new ScheduleRecordKey(currentRequest.ParentHandleId, currentRequest.ParentRequestId);
            ScheduleRecord record = new ScheduleRecord(recordKey, parentKey, nodeUsed, currentRequest.ProjectFileName,
                                                       currentRequest.ToolsetVersion, currentRequest.TargetNames);

            lock (scheduleTableLock)
            {
                ErrorUtilities.VerifyThrow(!handleIdToScheduleRecord.ContainsKey(recordKey),
                           "Schedule record should not be in the table");

                handleIdToScheduleRecord.Add(recordKey, record);

                // The ParentHandleId is an invalidEngineHandle when the host is the one who created
                // the current request
                if (currentRequest.ParentHandleId != EngineCallback.invalidEngineHandle)
                {
                    ErrorUtilities.VerifyThrow(handleIdToScheduleRecord.ContainsKey(parentKey),
                                               "Parent schedule record should be in the table");
                    ScheduleRecord parentRecord = handleIdToScheduleRecord[parentKey];
                    if (!parentRecord.Blocked)
                    {
                        blockedRequestsPerNode[parentRecord.EvaluationNode]++;
                    }
                    parentRecord.AddChildRecord(record);
                }
            }
        }

        /// <summary>
        /// This method is called when a build request is completed on a particular node. NodeId is never used instead we look up the node from the build request
        /// and the schedule record table
        /// </summary>
        internal void NotifyOfBuildResult(int nodeId, BuildResult buildResult)
        {
            if (!childMode && nodes.Length > 1)
            {
                // Ignore host requests
                if (buildResult.HandleId == EngineCallback.invalidEngineHandle)
                {
                    return;
                }

                ScheduleRecordKey recordKey = new ScheduleRecordKey(buildResult.HandleId, buildResult.RequestId);
                ScheduleRecord scheduleRecord = null;
                lock (scheduleTableLock)
                {
                    ErrorUtilities.VerifyThrow(handleIdToScheduleRecord.ContainsKey(recordKey),
                                               "Schedule record should be in the table");

                    scheduleRecord = handleIdToScheduleRecord[recordKey];
                    totalRequestsPerNode[scheduleRecord.EvaluationNode]--;
                    handleIdToScheduleRecord.Remove(recordKey);

                    if (scheduleRecord.ParentKey.HandleId != EngineCallback.invalidEngineHandle)
                    {
                        ErrorUtilities.VerifyThrow(handleIdToScheduleRecord.ContainsKey(scheduleRecord.ParentKey),
                                                   "Parent schedule record should be in the table");
                        ScheduleRecord parentRecord = handleIdToScheduleRecord[scheduleRecord.ParentKey];

                        // As long as there are child requests under the parent request the parent request is considered blocked
                        // Remove this build request from the list of requests the parent request is waiting on. This may unblock the parent request
                        parentRecord.ReportChildCompleted(recordKey);

                        // If completing the child request has unblocked the parent request due to all of the the Child requests being completed 
                        // decrement the number of blocked requests.
                        if (!parentRecord.Blocked)
                        {
                            blockedRequestsPerNode[parentRecord.EvaluationNode]--;
                        }
                    }
                }

                // Dump some interesting information to the console if profile build is turned on by an environment variable
                if (parentEngine.ProfileBuild && scheduleRecord != null && buildResult.TaskTime != 0 )
                {
                    Console.WriteLine("N " + scheduleRecord.EvaluationNode + " Name " + scheduleRecord.ProjectName + ":" +
                                      scheduleRecord.ParentKey.HandleId + ":" + scheduleRecord.ParentKey.RequestId +
                                      " Total " + buildResult.TotalTime + " Engine " + buildResult.EngineTime + " Task " + buildResult.TaskTime);
                }
            }
        }

        /// <summary>
        /// Called when the engine is in the process of sending a buildRequest to a child node. The entire purpose of this method
        /// is to switch the traversal strategy of the systems if there are nodes which do not have enough work availiable to them.
        /// </summary>
        internal void NotifyOfBuildRequest(int nodeIndex, BuildRequest currentRequest, int parentHandleId)
        {
            // This will only be null when the scheduler is instantiated on a child process in which case the initialize method
            // of the scheduler will not be called and therefore not initialize totalRequestsPerNode.
            if (totalRequestsPerNode != null)
            {
                // Check if it makes sense to switch from one traversal strategy to the other
                if (parentEngine.NodeManager.TaskExecutionModule.UseBreadthFirstTraversal)
                {
                    // Check if a switch to depth first traversal is in order
                    bool useBreadthFirstTraversal = false;
                    for (int i = 0; i < totalRequestsPerNode.Length; i++)
                    {
                        // Continue using breadth-first traversal as long as the non-blocked work load for this node is below 
                        // the nodeWorkloadProjectCount or its postBlockCount is non-zero
                        if ((totalRequestsPerNode[i] - blockedRequestsPerNode[i]) < nodeWorkLoadProjectCount || postBlockCount[i] != 0 )
                        {
                            useBreadthFirstTraversal = true;
                            break;
                        }
                    }

                    if (!useBreadthFirstTraversal)
                    {
                        if (Engine.debugMode)
                        {
                             Console.WriteLine("Switching to depth first traversal because all node have workitems");
                        }
                        parentEngine.NodeManager.TaskExecutionModule.UseBreadthFirstTraversal = false;

                        // Switch to depth first and change the traversal strategy of the entire system by notifying all child nodes of the change
                        parentEngine.PostEngineCommand(new ChangeTraversalTypeCommand(false, false));
                    }
                }
            }
        }

        /// <summary>
        /// Called by the engine to indicate that a particular request is blocked waiting for another
        /// request to finish building a target.
        /// </summary>
        internal void NotifyOfBlockedRequest(BuildRequest currentRequest)
        {
            if (!childMode && nodes.Length > 1)
            {
                ScheduleRecordKey recordKey = new ScheduleRecordKey(currentRequest.HandleId, currentRequest.RequestId);

                // Ignore host requests
                if (currentRequest.HandleId == EngineCallback.invalidEngineHandle)
                {
                    return;
                }

                lock (scheduleTableLock)
                {
                    ErrorUtilities.VerifyThrow(handleIdToScheduleRecord.ContainsKey(recordKey),
                                               "Schedule record should be in the table");
                    handleIdToScheduleRecord[recordKey].Blocked = true;
                    blockedRequestsPerNode[handleIdToScheduleRecord[recordKey].EvaluationNode]++;
                }
            }
        }

        /// <summary>
        /// Called by the engine to indicate that a particular request is no longer blocked waiting for another
        /// request to finish building a target
        /// </summary>
        internal void NotifyOfUnblockedRequest(BuildRequest currentRequest)
        {
            if (!childMode && nodes.Length > 1)
            {
                ScheduleRecordKey recordKey = new ScheduleRecordKey(currentRequest.HandleId, currentRequest.RequestId);
                lock (scheduleTableLock)
                {
                    ErrorUtilities.VerifyThrow(handleIdToScheduleRecord.ContainsKey(recordKey),
                                               "Schedule record should be in the table");
                    handleIdToScheduleRecord[recordKey].Blocked = false;
                    blockedRequestsPerNode[handleIdToScheduleRecord[recordKey].EvaluationNode]--;
                }
            }
        }

        /// <summary>
        /// Called by the engine to indicate that a node has run out of work
        /// </summary>
        /// <param name="nodeIndex"></param>
        internal void NotifyOfBlockedNode(int nodeId)
        {
            if (Engine.debugMode)
            {
                Console.WriteLine("Switch to breadth first traversal is requested by " + nodeId);
            }

            postBlockCount[nodeId] = nodeWorkLoadProjectCount/2;
        }

        /// <summary>
        /// Used by the introspector to dump the state when the nodes are being shutdown due to an error.
        /// </summary>
        internal void DumpState()
        {
            for (int i = 0; i < totalRequestsPerNode.Length; i++)
            {
                Console.WriteLine("Node " + i + " Outstanding " + totalRequestsPerNode[i] + " Blocked " + blockedRequestsPerNode[i]);
            }

            foreach (ScheduleRecordKey key in handleIdToScheduleRecord.Keys)
            {
                ScheduleRecord record = handleIdToScheduleRecord[key];
                Console.WriteLine(key.HandleId  + ":" + key.RequestId + " " + record.ProjectName + " on node " + record.EvaluationNode);
            }
        }
        #endregion

        #region Data

        /// <summary>
        /// NodeId of the engine who instantiated the scheduler. This is used to determine if a
        /// BuildRequest should be build locally as the project has already been loaded on this node.
        /// </summary>
        private int localNodeId;

        /// <summary>
        /// An array of nodes to which the scheduler can schedule work.
        /// </summary>
        private INodeDescription[] nodes;

        /// <summary>
        /// Counts the total number of outstanding requests (no result has been seen for the request) for a node.
        /// This is incremented in NotifyOfSchedulingDecision when a request it given to a node
        /// and decremented in NotifyOfBuildResult when results are returned (posted) from a node.
        /// </summary>
        private int[] totalRequestsPerNode;

        /// <summary>
        /// The number of BuildRequests blocked waiting for results for each node.
        /// This will be incremented once when a build request is scheduled which was generated as part of a msbuild callback
        /// and once for each call to NotifyOfBlockedRequest.
        ///
        /// It is decremented for each call to NotifyOfUnblockedRequest and once all of the child requests have been fullfilled.
        /// </summary>
        private int[] blockedRequestsPerNode;

        /// <summary>
        /// Keeps track of how many projects need to be sent to a node after the node has told the scheduler it has run out of work.
        /// </summary>
        private int[] postBlockCount;

        /// <summary>
        /// Indicates the scheduler should balance work accross nodes.
        /// This is only true when the environment variable MSBUILDLOADBALANCE is not 0
        /// </summary>
        private bool useLoadBalancing;

        /// <summary>
        /// Lock object for the handleIdToScheduleRecord dictionary
        /// </summary>
        private object scheduleTableLock;

        /// <summary>
        /// Keep track of build requsts to determine how many requests are blocked waiting on other build requests to complete.
        /// </summary>
        private Dictionary<ScheduleRecordKey, ScheduleRecord> handleIdToScheduleRecord;

        /// <summary>
        /// Indicates the scheduler is instantiated on a child node. This is being determined by
        /// initializaing the variable to true in the constructor and then setting it to false in the
        /// initialize method (the initialize method will only be called on the parent engine)
        /// </summary>
        private bool childMode;

        /// <summary>
        /// Reference to the engine who instantiated the scheduler
        /// </summary>
        private Engine parentEngine;

        /// <summary>
        /// Number of requests a node should have in an unblocked state before the system switches to a depth first traversal strategy.
        /// </summary>
        private const int nodeWorkLoadProjectCount = 4;

        /// <summary>
        /// Used to calculate which node a build request should be sent to if the scheduler is operating in a round robin fashion.
        /// Each time a build request is scheduled to a node in CalculateNodeForBuildRequest the lastUsedNode is incremented.
        /// This value is then mod'd (%) with the number of nodes to alternate which node the next build request goes to.
        /// </summary>
        private int lastUsedNode;

        #endregion
    }
}
