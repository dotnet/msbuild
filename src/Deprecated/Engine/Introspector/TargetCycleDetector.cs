// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;       // for debugger display attribute

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;


namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used to construct and analyze the graph of inprogress targets in order to find
    /// cycles inside the graph. To find cycles a post order traversal is used to assign a post order 
    /// traversal to each node. Back edges indicate cycles in the graph and they can indentified by
    /// a link from lower index node to a higher index node. 
    /// 
    /// The graph arrives in pieces from individual nodes and needs to be stiched together by identifying
    /// the parent and child for each cross node link. To do that it is necessary to match up parent 
    /// build request for a child with and outstanding request from the parent (see LinkCrossNodeBuildRequests)
    /// </summary>
    internal class TargetCycleDetector
    {
        #region Constructors
        internal TargetCycleDetector(EngineLoggingServices engineLoggingService, EngineCallback engineCallback)
        {
            this.engineLoggingService = engineLoggingService;
            this.engineCallback = engineCallback;

            dependencyGraph = new Hashtable();
            outstandingExternalRequests = new Hashtable();
            cycleParent = null;
            cycleChild  = null;
        }
        #endregion

        #region Properties
        internal TargetInProgessState CycleEdgeParent
        {
            get
            {
                return this.cycleParent;
            }
        }

        internal TargetInProgessState CycleEdgeChild
        {
            get
            {
                return this.cycleChild;
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Add a information about an array of inprogress targets to the graph
        /// </summary>
        internal void AddTargetsToGraph(TargetInProgessState[] inprogressTargets)
        {
            if (inprogressTargets != null)
            {
                for (int i = 0; i < inprogressTargets.Length; i++)
                {
                    if (inprogressTargets[i] != null)
                    {
                        AddTargetToGraph(inprogressTargets[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Add a information about a given inprogress target to the graph
        /// </summary>
        private void AddTargetToGraph(TargetInProgessState inProgressTarget)
        {
            // Check if the target is already in the graph in which
            // case reuse the current object
            GraphNode targetNode = (GraphNode)dependencyGraph[inProgressTarget.TargetId];
            if (targetNode == null)
            {
                targetNode = new GraphNode(inProgressTarget);
                dependencyGraph.Add(inProgressTarget.TargetId, targetNode);
            }
            else
            {
                ErrorUtilities.VerifyThrow(targetNode.targetState == null, "Target should only be added once");
                targetNode.targetState = inProgressTarget;
            }

            // For each parent target - add parent links creating parent targets if necessary
            foreach (TargetInProgessState.TargetIdWrapper parentTarget in inProgressTarget.ParentTargets)
            {
                GraphNode parentNode = (GraphNode)dependencyGraph[parentTarget];
                if (parentNode == null)
                {
                    parentNode = new GraphNode(null);
                    dependencyGraph.Add(parentTarget, parentNode);
                }
                parentNode.children.Add(targetNode);
            }

            // For all outgoing requests add them to the list of outstanding requests for the system
            if (inProgressTarget.OutstandingBuildRequests != null)
            {
                // Since the nodeIndex is not serialized it is necessary to restore it after the request
                // travels across the wire
                for (int i = 0; i < inProgressTarget.OutstandingBuildRequests.Length; i++)
                {
                    inProgressTarget.OutstandingBuildRequests[i].NodeIndex = inProgressTarget.TargetId.nodeId;
                }
                outstandingExternalRequests.Add(inProgressTarget.TargetId, inProgressTarget.OutstandingBuildRequests);
            }

            // If the target has no parents mark it as root (such targets are created due to host requests)
            if (inProgressTarget.RequestedByHost)
            {
                targetNode.isRoot = true;
            }
        }

        /// <summary>
        /// Analyze the graph and try to find cycles. Returns true if a cycle is found.
        /// </summary>
        internal bool FindCycles()
        {
            // Add the edges for the cross node connections
            LinkCrossNodeBuildRequests();

            // Perform post-order traversal of the forest of directed graphs
            traversalCount = 0;

            // First try to perform the traversal from the roots (i.e nodes that are due to host requests)
            foreach (GraphNode node in dependencyGraph.Values)
            {
                if (node.isRoot == true && node.traversalIndex == GraphNode.InvalidIndex)
                {
                    BreadthFirstTraversal(node);
                }
            }
            // Verify that all nodes have been reached 
            foreach (GraphNode node in dependencyGraph.Values)
            {
                if (node.traversalIndex == GraphNode.InvalidIndex)
                {
                    BreadthFirstTraversal(node);
                }
            }

            // Check every edge for being a back edge
            foreach (GraphNode node in dependencyGraph.Values)
            {
                // Check the edges from the current node to its children
                FindBackEdges(node);

                // Stop looking as soon as the first cycle is found
                if (cycleParent != null)
                {
                    break;
                }
            }

            return (cycleParent != null);
        }

        /// <summary>
        /// For each target that has a cross node build request waiting for it to complete, iterate
        /// over the list of outstanding requests and find the matching out going request. Once
        /// the matching request is found - link the parent and child targets.
        /// </summary>
        private void LinkCrossNodeBuildRequests()
        {
            foreach (GraphNode node in dependencyGraph.Values)
            {
                TargetInProgessState.TargetIdWrapper[] parentsForBuildRequests = 
                    new TargetInProgessState.TargetIdWrapper[node.targetState.ParentBuildRequests.Count];

                for (int j = 0; j < node.targetState.ParentBuildRequests.Count; j++ )
                {
                    BuildRequest buildRequest = node.targetState.ParentBuildRequests[j];
                    int nodeIndex = buildRequest.NodeIndex;
                    int handleId = buildRequest.HandleId;
                    int requestId = buildRequest.RequestId;
                    bool foundParent = false;

                    // Skip requests that originated from the host
                    if (handleId == EngineCallback.invalidEngineHandle)
                    {
                        node.isRoot = true;
                        continue;
                    }

                    // If the request being analyzed came from one of the child nodes, its incoming external request's
                    // handleId will point at a routing context on the parent engine. If the outgoing request 
                    // orginated from another child the two requests (outgoing and incoming) point at different 
                    // routing contexts. In that case it is necessary to unwind the incoming request to the routing 
                    // context of the outgoing request. If outgoing request originated from the parent node - 
                    // there will be only one routing request.
                    if (node.targetState.TargetId.nodeId != 0)
                    {
                        ExecutionContext executionContext = engineCallback.GetExecutionContextFromHandleId(buildRequest.HandleId);
                        RequestRoutingContext routingContext = executionContext as RequestRoutingContext;
                        if (routingContext != null && routingContext.ParentHandleId != EngineCallback.invalidEngineHandle)
                        {
                            ExecutionContext nextExecutionContext = engineCallback.GetExecutionContextFromHandleId(routingContext.ParentHandleId);

                            if (nextExecutionContext is RequestRoutingContext)
                            {
                                nodeIndex   = nextExecutionContext.NodeIndex;
                                handleId = routingContext.ParentHandleId;
                                requestId   = routingContext.ParentRequestId;
                            }
                        }
                        else
                        {
                            // Skip requests that originated from the host
                            node.isRoot = true;
                            continue;
                        }
                    }

                    // Iterate over all outstanding requests until a match is found
                    foreach (DictionaryEntry entry in outstandingExternalRequests)
                    {
                        BuildRequest[] externalRequests = (BuildRequest[])entry.Value;
                        for (int i = 0; i < externalRequests.Length && !foundParent; i++)
                        {
                            if (handleId == externalRequests[i].HandleId &&
                                requestId == externalRequests[i].RequestId &&
                                nodeIndex == externalRequests[i].NodeIndex)
                            {
                                // Verify that the project name is the same
                                ErrorUtilities.VerifyThrow(
                                    String.Compare(buildRequest.ProjectFileName, externalRequests[i].ProjectFileName, StringComparison.OrdinalIgnoreCase) == 0,
                                    "The two requests should have the same project name");

                                // Link the two graph nodes together
                                GraphNode parentNode = (GraphNode)dependencyGraph[entry.Key];
                                parentNode.children.Add(node);

                                parentsForBuildRequests[j] = parentNode.targetState.TargetId;

                                foundParent = true;
                            }
                        }

                        if (foundParent)
                        {
                            break;
                        }
                    }
                }
                node.targetState.ParentTargetsForBuildRequests = parentsForBuildRequests;
            }
        }

        /// <summary>
        /// Breadth first traversal over the DAG, assigning post order indecies to each node in the graph. This
        /// function should be called at least once for each tree in the forest in order to assign
        /// indecies to every node in the graph
        /// </summary>
        private void BreadthFirstTraversal(GraphNode node)
        {
            ErrorUtilities.VerifyThrow(node.traversalIndex == GraphNode.InvalidIndex,
                                        "Should only consider each node once");

            node.traversalIndex = GraphNode.InProgressIndex;

            for (int i = 0; i < node.children.Count; i++)
            {
                if (node.children[i].traversalIndex == GraphNode.InvalidIndex)
                {
                    BreadthFirstTraversal(node.children[i]);
                }
            }

            node.traversalIndex = traversalCount;
            traversalCount++;
        }

        /// <summary>
        /// Check for back edges from the given node to its children
        /// </summary>
        private void FindBackEdges(GraphNode node)
        {
            ErrorUtilities.VerifyThrow(node.traversalIndex != GraphNode.InvalidIndex,
                                       "Each node should have a valid traversal index");

            for (int i = 0; i < node.children.Count; i++)
            {
                // Check for a back edge
                if (node.children[i].traversalIndex > node.traversalIndex )
                {
                    cycleParent = node.targetState;
                    cycleChild  = node.children[i].targetState;
                    DumpCycleSequence(node.children[i], node);
                    break;
                }
                // Check for an edge from the node to itself
                if (node.children[i].targetState.TargetId == node.targetState.TargetId)
                {
                    cycleParent = node.targetState;
                    cycleChild  = node.targetState;
                    break;
                }
            }
        }

        private void DumpCycleSequence(GraphNode parent, GraphNode child)
        {
            foreach (GraphNode node in dependencyGraph.Values)
            {
                node.traversalIndex = GraphNode.InvalidIndex;
            }
            BuildEventContext buildEventContext = 
                new BuildEventContext(child.targetState.TargetId.nodeId,
                                 child.targetState.TargetId.id, 
                                 BuildEventContext.InvalidProjectContextId, 
                                 BuildEventContext.InvalidTaskId
                                );
            DumpCycleSequenceOutput(parent, child, buildEventContext);
        }

        private bool DumpCycleSequenceOutput(GraphNode parent, GraphNode child, BuildEventContext buildEventContext)
        {
            if (parent == child )
            {
                engineLoggingService.LogComment(buildEventContext, "cycleTraceTitle");
                engineLoggingService.LogComment
                    (buildEventContext, "cycleTraceLine", parent.targetState.TargetId.name, parent.targetState.ProjectName);
                return true;
            }

            if (parent.traversalIndex == GraphNode.InProgressIndex)
            {
                return false;
            }

            parent.traversalIndex = GraphNode.InProgressIndex;

            for (int i = 0; i < parent.children.Count; i++)
            {
                if (DumpCycleSequenceOutput(parent.children[i], child, buildEventContext))
                {
                    engineLoggingService.LogComment
                        (buildEventContext, "cycleTraceLine", parent.targetState.TargetId.name, parent.targetState.ProjectName);
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Data
        /// <summary>
        /// The table of all targets in the dependency graph, indexed by TargetNameStructure which
        /// contains Target name, Project Id, Node Id which uniquely identifies every target in the system
        /// </summary>
        private Hashtable dependencyGraph;
        /// <summary>
        /// List of all outstanding cross node build requests
        /// </summary>
        private Hashtable outstandingExternalRequests;
        /// <summary>
        /// The index used for the breadth first traversal
        /// </summary>
        private int traversalCount;
        /// <summary>
        /// The TargetNameStructure for the parent of the edge creating the cycle
        /// </summary>
        private TargetInProgessState cycleParent;
        /// <summary>
        /// The TargetNameStructure for the parent of the edge creating the cycle
        /// </summary>
        private TargetInProgessState cycleChild;
        /// <summary>
        /// Logging service for outputing the loop trace
        /// </summary>
        private EngineLoggingServices engineLoggingService;
        /// <summary>
        /// Engine callback which is to walk the inprogress execution contexts
        /// </summary>
        private EngineCallback engineCallback;
        #endregion

        [DebuggerDisplay("Node (Name = { targetState.TargetId.name }, Project = { targetState.TargetId.projectId }), Node = { targetState.TargetId.nodeId })")]
        private class GraphNode
        {
            #region Constructors
            internal GraphNode(TargetInProgessState targetState)
            {
                this.targetState = targetState;
                this.children = new List<GraphNode>();
                this.traversalIndex = InvalidIndex;
                this.isRoot = false;
            }
            #endregion

            #region Data
            internal TargetInProgessState targetState;
            internal List<GraphNode> children;
            internal bool isRoot;
            internal int traversalIndex;

            internal const int InvalidIndex    = -1;
            internal const int InProgressIndex = -2;
            #endregion
        }
    }
}
