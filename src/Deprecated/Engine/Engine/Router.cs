// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This call interfaces with the scheduler and notifies it of events of interest such as build results or
    /// build requests. It also routes the build results appropriately depending on if it is running on the child or
    /// on the parent.
    /// </summary>
    internal class Router
    {
        #region Constructors
        /// <summary>
        /// Private constructor to avoid parameterless instantiation
        /// </summary>
        private Router()
        {
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        internal Router(Engine parentEngine, Scheduler scheduler)
        {
            this.nodeManager = parentEngine.NodeManager;
            this.parentEngine = parentEngine;
            this.scheduler = scheduler;

            this.childMode = false;
            this.parentNode = null;
        }
        #endregion

        #region Properties

        /// <summary>
        /// Returns true on the child engine and false otherwise. this is used by the engine to determine if the engine is running on a child 
        /// process or not. The childMode is set to true in the NodeLocalEngineLoop which is only executed on a child process.
        /// </summary>
        internal bool ChildMode
        {
            get
            {
                return this.childMode;
            }
            set
            {
                this.childMode = value;
            }
        }

        /// <summary>
        /// Returns null on the parent engine and a pointer to the node hosting the engine on the child
        /// engines
        /// </summary>
        internal Node ParentNode
        {
            get
            {
                return this.parentNode;
            }
            set
            {
                this.parentNode = value;
            }
        }

        /// <summary>
        /// Used by the engine to choose more effecient code path for single proc
        /// execution. In general the usage should be minimized by using inheretence and
        /// different classes in single proc and multiproc cases
        /// </summary>
        internal bool SingleThreadedMode
        {
            get
            {
                return !childMode && nodeManager.MaxNodeCount == 1;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This method creates a BuildResult using the information contained in a completed build request and
        /// then routes it to the right node. On a child process, this means either consume the result localy,
        /// or send it to the parent node. On a parent node, this means either consume the result locally or 
        /// send it to a child node
        /// </summary>
        internal void PostDoneNotice(BuildRequest buildRequest)
        {
            // Create a container with the results of the evaluation 
            BuildResult buildResult = buildRequest.GetBuildResult();

            // If we're supposed to use caching and this request wasn't restored from cache, cache it
            if (buildRequest.UseResultsCache && !buildRequest.RestoredFromCache)
            {
                CacheScope cacheScope = parentEngine.CacheManager.GetCacheScope(buildRequest.ProjectFileName, buildRequest.GlobalProperties, buildRequest.ToolsetVersion, CacheContentType.BuildResults);
                cacheScope.AddCacheEntryForBuildResults(buildResult);
            }

            // an external request is any request that came from the parent engine, all requests to a child are external 
            // unless the project was alredy loaded on the node itself
            if (buildRequest.IsExternalRequest)
            {
                // If the build request was send from outside the current process,
                // send the results to the parent engine
                parentNode.PostBuildResultToHost(buildResult);
            }
            else
            {
                // In the case of a child process, getting to this point means the request will be satisfied locally, the node index should be 0
                // on the parent engine however, the node index can be, 0 for the local node, or can be >0 which represents a child node
                PostDoneNotice(buildRequest.NodeIndex, buildResult);
            }
        }
        /// <summary>
        /// Route a given BuildResult to a given node.
        /// </summary>
        internal void PostDoneNotice(int nodeId, BuildResult buildResult)
        {
               
            
                // Notify the scheduler that a given node(nodeId) will be getting a buildResult.
                // This method is a no-op if the router is on a child process
                scheduler?.NotifyOfBuildResult(nodeId, buildResult);
            

            if (nodeId == EngineCallback.inProcNode)
            {
                // Make a deep copy of the results. This is only necessary if nodeId = EngineCallback.inProcNode because
                // that's the only case that this buildResult wouldn't get copied anyway to serialize
                // it for the wire. A copy is necessary because the cache may contain the results and the user
                // task code should not have pointers to the same copy
                buildResult = new BuildResult(buildResult, true);
            }

            // Give the result to the node manager which will send the result to the correct node.
            nodeManager.PostBuildResultToNode(nodeId, buildResult);
        }

        /// <summary>
        /// This method is called once the engine has decided to sent a build request to a child node.
        /// Route the given BuildRequest to the given node. If necessary a routing context is 
        /// created to manage future communication with the node regarding the build request.
        /// </summary>
        internal void PostBuildRequest(BuildRequest currentRequest, int nodeIndex)
        {
            // if the request is to be sent to the parent node, post the request back to the host
            if (nodeIndex == EngineCallback.parentNode)
            {
                ParentNode.PostBuildRequestToHost(currentRequest);
            }
            else
            {
                // Dont create any contexts if the request was supposed to be processed on the current node
                if (nodeIndex != EngineCallback.inProcNode)
                {
                    // Get the cache scope for the request (possibly creating it). The cache scope will contain the taskoutputs of the build request
                    // which can be reused of the same project/toolsversion/globalproperties is asked for again.
                    CacheScope cacheScope = parentEngine.CacheManager.GetCacheScope(currentRequest.ProjectFileName, currentRequest.GlobalProperties, currentRequest.ToolsetVersion, CacheContentType.BuildResults);

                    // Create a routing context and update the request to refer to the new node handle id
                    int parentHandleId = currentRequest.HandleId;
                    currentRequest.HandleId =
                        parentEngine.EngineCallback.CreateRoutingContext
                                    (nodeIndex, currentRequest.HandleId, currentRequest.NodeIndex,
                                     currentRequest.RequestId, cacheScope, currentRequest, null);

                       
                    
                        // Check to see if we need to change the traversal strategy of the system
                        // parentHandleId and node index are not used in the function so it can be ignored
                        scheduler?.NotifyOfBuildRequest(nodeIndex, currentRequest, parentHandleId);
                    
                    
                    nodeManager.PostBuildRequestToNode(nodeIndex, currentRequest);
                }
            }
        }

        #endregion

        #region Data
        /// <summary>
        /// The node manager is used as a proxy for communication with child nodes
        /// </summary>
        NodeManager nodeManager;

        /// <summary>
        /// The parent engine who instantiated the router
        /// </summary>
        Engine parentEngine;

        /// <summary>
        /// Scheduler who is responsible for determining which nodes a build request should be sent to.
        /// </summary>
        Scheduler scheduler;

        /// <summary>
        /// Is the router instantiated on a child process
        /// </summary>
        bool childMode;

        /// <summary>
        /// What is the parent Node on which the engine is hosted if we are a child process
        /// </summary>
        Node parentNode;
        #endregion 
    }
}
