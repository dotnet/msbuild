// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Xml;
using System.Globalization;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is an external representation of engine communication with the TEM or child nodes.
    /// </summary>
    internal class EngineCallback : IEngineCallback
    {
        #region Constructors
        /// <summary>
        /// Creates a callback class. There should only be one callback per engine under normal
        /// circumstances.
        /// </summary>
        internal EngineCallback(Engine parentEngine)
        {
            this.parentEngine = parentEngine;
        }
        #endregion

        #region Methods for accessing engine internals from the node

        /// <summary>
        /// This method is called by the node to request evaluation of a target that was
        /// requested by a task via IBuildEngine interface. It posts the
        /// request into a queue in the engine
        /// </summary>
        /// <param name="buildRequests"></param>
        public void PostBuildRequestsToHost(BuildRequest[] buildRequests)
        {
            if (buildRequests.Length > 0)
            {
                // We can safely assume that all requests need to be routed to the same engine because
                // they originated from the same task 
                for(int i = 0; i < buildRequests.Length; i++)
                {
                    ProcessBuildRequest(buildRequests[i]);
                }

                parentEngine.PostBuildRequests(buildRequests);
            }
        }

        /// <summary>
        /// Called on the main node only.
        /// </summary>
        public Exception PostCacheEntriesToHost(int nodeId, CacheEntry[] entries, string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion, CacheContentType cacheContentType)
        {
            try
            {
                parentEngine.CacheManager.SetCacheEntries(entries, scopeName, scopeProperties, scopeToolsVersion, cacheContentType);
            }
            catch (InvalidOperationException e)
            {
                return e;
            }

            return null;
        }

        /// <summary>
        /// Called on the main node only.
        /// </summary>
        public CacheEntry[] GetCachedEntriesFromHost(int nodeId, string[] names, string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion, CacheContentType cacheContentType)
        {
            return parentEngine.CacheManager.GetCacheEntries(names, scopeName, scopeProperties, scopeToolsVersion, cacheContentType);
        }

        private void ProcessBuildRequest(BuildRequest buildRequest)
        {
            ExecutionContext executionContext = GetExecutionContextFromHandleId(buildRequest.HandleId);
            // Restore the requests non-serialized data to the correct state
            buildRequest.RestoreNonSerializedDefaults();
            buildRequest.NodeIndex = executionContext.NodeIndex;

            ErrorUtilities.VerifyThrow(buildRequest.ParentBuildEventContext != null, "Should not have a null parentBuildEventContext");
            ErrorUtilities.VerifyThrow(buildRequest.IsGeneratedRequest, "Should not be sending a non generated request from the child node to the parent node");

            // For buildRequests originating from the TEM  - additional initialization is necessary
            TaskExecutionContext taskExecutionContext = executionContext as TaskExecutionContext;
            if (taskExecutionContext != null)
            {
                Project parentProject = taskExecutionContext.ParentProject;
                buildRequest.ParentHandleId = taskExecutionContext.TriggeringBuildRequest.HandleId;
                buildRequest.ParentRequestId = taskExecutionContext.TriggeringBuildRequest.RequestId;

                if (buildRequest.ToolsetVersion == null && parentProject.OverridingToolsVersion)
                {
                    // If the MSBuild task (or whatever) didn't give us a specific tools version,
                    // but the parent project is using an overridden tools version, then use that one
                    buildRequest.ToolsetVersion = parentProject.ToolsVersion;
                }

                try
                {
                    if (buildRequest.GlobalProperties == null)
                    {
                        try
                        {
                            // Make sure we have a blank global properties because if there is a problem merging them we wont have a crash when we try and cache the build result.
                            buildRequest.GlobalProperties = new BuildPropertyGroup();
                            buildRequest.GlobalProperties =
                                parentEngine.MergeGlobalProperties(parentProject.GlobalProperties, null,
                                                                   buildRequest.ProjectFileName,
                                                                   buildRequest.GlobalPropertiesPassedByTask);
                        }
                        catch (ArgumentException e)
                        {
                            ConvertToInvalidProjectException(buildRequest, parentProject, e);
                        }
                        catch (InvalidOperationException e)
                        {
                            ConvertToInvalidProjectException(buildRequest, parentProject, e);
                        }
                    }

                    // We need to figure out which project object this request is refering to
                    if (buildRequest.ProjectFileName == null)
                    {
                        ErrorUtilities.VerifyThrow(parentProject != null, "Parent project must be non-null");

                        // This means the caller (the MSBuild task) wants us to use the same project as the calling 
                        // project.  This allows people to avoid passing in the Projects parameter on the MSBuild task.
                        Project projectToBuild = parentProject;

                        // If the parent project (the calling project) already has the same set of global properties
                        // as what is being requested, just re-use it.  Otherwise, we need to instantiate a new
                        // project object that has the same project contents but different global properties.
                        if (!projectToBuild.GlobalProperties.IsEquivalent(buildRequest.GlobalProperties) &&
                            (String.Equals(parentProject.ToolsVersion, buildRequest.ToolsetVersion, StringComparison.OrdinalIgnoreCase)))
                        {
                            projectToBuild = parentEngine.GetMatchingProject(parentProject,
                                                 parentProject.FullFileName, buildRequest.GlobalProperties,
                                                 buildRequest.ToolsetVersion, buildRequest.TargetNames, buildRequest.ParentBuildEventContext, buildRequest.ToolsVersionPeekedFromProjectFile);
                        }
                        buildRequest.ProjectToBuild = projectToBuild;
                        buildRequest.ProjectFileName = projectToBuild.FullFileName;
                        buildRequest.FireProjectStartedFinishedEvents = false;
                    }
                }
                catch (InvalidProjectFileException e)
                {
                    buildRequest.BuildCompleted = true;
                    // Store message so it can be logged by the engine build loop
                    buildRequest.BuildException = e;
                }
            }
            else
            {
                RequestRoutingContext requestRoutingContext = executionContext as RequestRoutingContext;
                buildRequest.ParentHandleId = requestRoutingContext.ParentHandleId;
                buildRequest.ParentRequestId = requestRoutingContext.ParentRequestId;
            }
        }

        /// <summary>
        /// If there is an exception in process build request we will wrap it in an invalid project file exception as any exceptions caught here are really problems with a project file
        /// this exception will be handled in the engine and logged
        /// </summary>
        private static void ConvertToInvalidProjectException(BuildRequest buildRequest, Project parentProject, Exception e)
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo(buildRequest.ProjectFileName);
            throw new InvalidProjectFileException(parentProject.FullFileName, fileInfo.Line, fileInfo.Column, fileInfo.EndLine, fileInfo.EndColumn, e.Message, null, null, null);
        }

        /// <summary>
        /// This method is used by the node to post the task outputs to the engine.
        /// Items and properties output by the task return to the engine thread via the Lookup the
        /// TaskEngine was passed, not via posting to the queue here.
        /// </summary>
        internal void PostTaskOutputs
        (
            int handleId,
            bool taskExecutedSuccessfully,
            Exception thrownException,
            long executionTime
        )
        {
            TaskExecutionContext executionContext = GetTaskContextFromHandleId(handleId);
            // Set the outputs on the context
            executionContext.SetTaskOutputs(taskExecutedSuccessfully, thrownException, executionTime);
            // Submit it to the queue
            parentEngine.PostTaskOutputUpdates(executionContext);
        }

        /// <summary>
        /// This method is used by the child node to post results of a build request back to the
        /// parent node. The parent node then decides if need to re-route the results to another node
        /// that requested the evaluation or if it will consume the result locally
        /// </summary>
        /// <param name="buildResult"></param>
        public void PostBuildResultToHost(BuildResult buildResult)
        {
            RequestRoutingContext routingContext = GetRoutingContextFromHandleId(buildResult.HandleId);
            ErrorUtilities.VerifyThrow(routingContext.CacheScope != null, "Cache scope should be created for this context");

            // Cache the results
            routingContext.CacheScope.AddCacheEntryForBuildResults(buildResult);

            if (Engine.debugMode)
            {
                Console.WriteLine("Received result for HandleId " + buildResult.HandleId + ":" + buildResult.RequestId + " mapped to " + routingContext.ParentHandleId + ":" + routingContext.ParentRequestId);
            }

            // Update the results with the original handle id and request id, so that 
            buildResult.HandleId = routingContext.ParentHandleId;

            // If the build result is created from a generated build request a done notice should be posted as other targets could be waiting for this target to finish
            if (buildResult.HandleId != invalidEngineHandle)
            {
                buildResult.RequestId = routingContext.ParentRequestId;
                parentEngine.Router.PostDoneNotice(routingContext.ParentNodeIndex, buildResult);
            }
            else // The build results need to be stored into the build request so they can be sent back to the host that requested the build
            {
                routingContext.TriggeringBuildRequest.OutputsByTarget = buildResult.OutputsByTarget;
                routingContext.TriggeringBuildRequest.BuildSucceeded = buildResult.EvaluationResult;
                routingContext.TriggeringBuildRequest.BuildCompleted = true;
                parentEngine.PostEngineCommand(new HostBuildRequestCompletionEngineCommand());
            }

            // At this point the execution context we created for the execution of this build request can be deleted
            lock (freedContexts)
            {
                freedContexts.Add(routingContext);
            }
        }

        /// <summary>
        /// Called either on the main or child node. This is the routing method for setting cache entries.
        /// </summary>
        public void SetCacheEntries
        (
            int handleId, CacheEntry[] entries,
            string cacheScope, string cacheKey, string cacheVersion,
            CacheContentType cacheContentType, bool localNodeOnly
        )
        {
            TaskExecutionContext executionContext = GetTaskContextFromHandleId(handleId);
            BuildPropertyGroup scopeProperties;

            if (cacheKey == null)
            {
                Project parentProject = executionContext.ParentProject;
                scopeProperties = parentProject.GlobalProperties;
            }
            else
            {
                // Property values are compared using case sensitive comparisons because the case of property values do have meaning.
                // In this case we are using properties in a manner where we do not want case sensitive comparisons.
                // There is not enough benefit for this one special case to add case insensitive 
                // comparisons to build properties. We instead uppercase all of the keys for both get and set CachedEntries.
                scopeProperties = new BuildPropertyGroup();
                scopeProperties.SetProperty("CacheKey", cacheKey.ToUpper(CultureInfo.InvariantCulture));
            }

            if (cacheScope == null)
            {
                cacheScope = executionContext.ParentProject.FullFileName;
            }

            if (cacheVersion == null)
            {
                cacheVersion = executionContext.ParentProject.ToolsVersion;
            }

            parentEngine.CacheManager.SetCacheEntries(entries, cacheScope, scopeProperties, cacheVersion, cacheContentType);

            // Also send these to the parent if we're allowed to
            if (parentEngine.Router.ChildMode && !localNodeOnly)
            {
                Exception exception = parentEngine.Router.ParentNode.PostCacheEntriesToHost(entries, cacheScope, scopeProperties, cacheVersion, cacheContentType);

                // If we had problems on the parent node, rethrow the exception here
                if (exception != null)
                {
                    throw exception;
                }
            }
        }

        /// <summary>
        /// Called either on the main or child node. This is the routing method for getting cache entries.
        /// </summary>
        public CacheEntry[] GetCacheEntries
        (
            int handleId, string[] names,
            string cacheScope, string cacheKey, string cacheVersion,
            CacheContentType cacheContentType, bool localNodeOnly
        )
        {
            TaskExecutionContext executionContext = GetTaskContextFromHandleId(handleId);
            BuildPropertyGroup scopeProperties;

            if (cacheKey == null)
            {
                Project parentProject = executionContext.ParentProject;
                scopeProperties = parentProject.GlobalProperties;
            }
            else
            {
                // Property values are compared using case sensitive comparisons because the case of property values do have meaning.
                // In this case we are using properties in a manner where we do not want case sensitive comparisons.
                // There is not enough benefit for this one special case to add case insensitive 
                // comparisons to build properties. We instead uppercase all of the keys for both get and set CachedEntries.
                scopeProperties = new BuildPropertyGroup();
                scopeProperties.SetProperty("CacheKey", cacheKey.ToUpper(CultureInfo.InvariantCulture));
            }

            if (cacheScope == null)
            {
                cacheScope = executionContext.ParentProject.FullFileName;
            }

            if (cacheVersion == null)
            {
                cacheVersion = executionContext.ParentProject.ToolsVersion;
            }

            CacheEntry[] result = parentEngine.CacheManager.GetCacheEntries(names, cacheScope, scopeProperties, cacheVersion, cacheContentType);

            bool haveCompleteResult = (result.Length == names.Length);

            if (haveCompleteResult)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    if (result[i] == null)
                    {
                        haveCompleteResult = false;
                        break;
                    }
                }
            }

            // If we didn't have the complete result locally, check with the parent if allowed.
            if (!haveCompleteResult && parentEngine.Router.ChildMode && !localNodeOnly)
            {
                result = parentEngine.Router.ParentNode.GetCachedEntriesFromHost(names, cacheScope, scopeProperties, cacheVersion, cacheContentType);
                parentEngine.CacheManager.SetCacheEntries(result, cacheScope, scopeProperties, cacheVersion, cacheContentType);
            }

            return result;
        }

        /// <summary>
        /// Submit the logging message to the engine queue. Note that we are currently not utilizing the
        /// handleId, but plan to do so in the future to fill out the data structure passed to the engine
        /// </summary>
        public void PostLoggingMessagesToHost(int nodeId, NodeLoggingEvent[] nodeLoggingEventArray)
        {
            // We can safely assume that all messages need to be routed to the same engine because
            // they originated from the same task. This is true as long as we don't allow multiple engines within 
            // a single process to utilize external nodes.
            if (nodeLoggingEventArray.Length > 0)
            {
                parentEngine.LoggingServices.PostLoggingEvents(nodeLoggingEventArray);
            }
        }

        /// <summary>
        /// Figure out the line and column number of the task XML node in the original
        /// project context
        /// </summary>
        internal void GetLineColumnOfXmlNode(int handleId, out int lineNumber, out int columnNumber)
        {
            TaskExecutionContext executionContext = GetTaskContextFromHandleId(handleId);
            XmlSearcher.GetLineColumnByNode(executionContext.TaskNode, out lineNumber, out columnNumber);
        }

        /// <summary>
        /// Gets the default engine task registry. If the TEM runs out-of proc with the engine we should send the task declarations for all the default tasks parsed out of the *.tasks XML instead.
        /// </summary>
        /// <returns>The default engine task registry.</returns>
        internal ITaskRegistry GetEngineTaskRegistry(int handleId)
        {
            TaskExecutionContext executionContext = GetTaskContextFromHandleId(handleId);
            return parentEngine.GetTaskRegistry(executionContext.BuildEventContext,
                                    executionContext.ParentProject.ToolsVersion);
        }

        /// <summary>
        /// Gets the project task registry. If the TEM runs out-of proc with the engine we should send the task declarations for all the using tasks parsed out of project XML instead.
        /// </summary>
        /// <returns>The default engine task registry.</returns>
        internal ITaskRegistry GetProjectTaskRegistry(int handleId)
        {
            TaskExecutionContext executionContext = GetTaskContextFromHandleId(handleId);
            return executionContext.ParentProject.TaskRegistry;
        }

        /// <summary>
        /// Get the version of the toolset used by the project
        /// </summary>
        /// <param name="handleId"></param>
        /// <returns></returns>
        internal string GetToolsPath(int handleId)
        {
            TaskExecutionContext executionContext = GetTaskContextFromHandleId(handleId);
            return parentEngine.ToolsetStateMap[executionContext.ParentProject.ToolsVersion].ToolsPath;
        }

        /// <summary>
        /// This method is called to post the status of the node
        /// </summary>
        public void PostStatus(int nodeId, NodeStatus nodeStatus, bool blockUntilSent)
        {
            parentEngine.PostNodeStatus(nodeId, nodeStatus);
        }

        /// <summary>
        /// This method is only used in by the inproc node
        /// </summary>
        internal Engine GetParentEngine()
        {
            return parentEngine;
        }

        /// <summary>
        /// This method converts a list handles to inprogress contexts into a list of target objects
        /// </summary>
        internal Target[] GetListOfTargets(int[] handleIds)
        {
            Target[] targets = new Target[handleIds.Length];

            for (int i = 0; i < handleIds.Length; i++)
            {
                TaskExecutionContext executionContext = GetTaskContextFromHandleId(handleIds[i]);
                if (executionContext != null)
                {
                    targets[i] = executionContext.ParentTarget;
                }
                else
                {
                    targets[i] = null;
                }
            }

            return targets;
        }

        #endregion

        #region Methods for managing execution contexts

        /// <summary>
        /// Given a handleId, this method returns the corresponding ExecutionContext. This
        /// context contains only value type data and can be used from any domain.
        /// </summary>
        internal ExecutionContext GetExecutionContextFromHandleId(int handleId)
        {
            // We don't need to lock the hashtable because it is thread safe for multiple readers
            return (ExecutionContext)executionContexts[handleId];
        }

        /// <summary>
        /// Given a handleId, this method returns the corresponding RequestRoutingContext. This context
        /// contains some data (such as parent projet or parent target) which should only be accessed from
        /// within the engine domain.
        /// </summary>
        internal TaskExecutionContext GetTaskContextFromHandleId(int handleId)
        {
            // We don't need to lock the hashtable because it is thread safe for multiple readers
            return (TaskExecutionContext)executionContexts[handleId];
        }

        /// <summary>
        /// Given a handleId, this method returns the corresponding RequestRoutingContext. This
        /// context contains only value type data and can be used from any domain
        /// </summary>
        internal RequestRoutingContext GetRoutingContextFromHandleId(int handleId)
        {
            // We don't need to lock the hashtable because it is thread safe for multiple readers
            return (RequestRoutingContext)executionContexts[handleId];
        }

        /// <summary>
        /// This method creates a new TaskExecutionContext and return a integer token that maps to it.
        /// This method is not thread safe and must be called only from the engine thread.
        /// </summary>
        internal int CreateTaskContext
        (
            Project parentProject,
            Target  parentTarget,
            ProjectBuildState buildContext,
            XmlElement taskNode,
            int nodeIndex,
            BuildEventContext taskContext
        )
        {
            int handleId = nextContextId;
            nextContextId++;

            TaskExecutionContext executionContext =
                new TaskExecutionContext(parentProject, parentTarget, taskNode, buildContext, handleId, nodeIndex, taskContext);

            executionContexts.Add(handleId, executionContext);

            return handleId;
        }

        /// <summary>
        /// This method creates a new routing context. This method is not thread safe and must be called
        /// only from the engine thread.
        /// </summary>
        internal int CreateRoutingContext
        (
            int nodeIndex,
            int parentHandleId,
            int parentNodeIndex,
            int parentRequestId,
            CacheScope cacheScope,
            BuildRequest triggeringBuildRequest,
            BuildEventContext buildEventContext
        )
        {
            int handleId = nextContextId;
            nextContextId++;

            RequestRoutingContext executionContext =
                new RequestRoutingContext(handleId, nodeIndex, parentHandleId, parentNodeIndex, parentRequestId,
                                          cacheScope, triggeringBuildRequest, buildEventContext);

            executionContexts.Add(handleId, executionContext);

            return handleId;
        }

        /// <summary>
        /// This method maps the given handleId to null. The entry will be later removed by the engine thread.
        /// </summary>
        internal void ClearContextState(int handleId)
        {
            if (handleId != invalidEngineHandle)
            {
                ErrorUtilities.VerifyThrow(executionContexts.ContainsKey(handleId), "The table must contain this entry");
                executionContexts.Remove(handleId);
            }

            // Check if there are freed contexts waiting to be deleted
            if (freedContexts.Count > freeListThreshold)
            {
                lock (freedContexts)
                {
                    foreach (ExecutionContext executionContext in freedContexts)
                    {
                        executionContexts.Remove(executionContext.HandleId);
                    }
                    freedContexts.Clear();
                }
            }
        }

        #endregion

        #region Constants
        /// <summary>
        /// Number assigned to an invalid engine handle, This handleId is used by Buildrequests
        /// to show they are a routing context
        /// </summary>
        internal const int invalidEngineHandle = -1;

        /// <summary>
        /// NodeId for an inproc node
        /// </summary>
        internal const int inProcNode = 0;

        /// <summary>
        /// NodeId for the parent node
        /// </summary>
        internal const int parentNode = -1;

        /// <summary>
        /// Invalid NodeId
        /// </summary>
        internal const int invalidNode = -2;
        #endregion

        #region Data
        /// <summary>
        /// This hashtable contains the all the executionContexts for the current process
        /// </summary>
        private Hashtable executionContexts = new Hashtable();
        /// <summary>
        /// List of contexts that should be removed from the hashtable by the engine thread
        /// </summary>
        private List<ExecutionContext> freedContexts = new List<ExecutionContext>(2*freeListThreshold);
        /// <summary>
        /// The counter used to generate unique identifiers for each context
        /// </summary>
        private int nextContextId = 0;
        /// <summary>
        /// The pointer to the engine to which this callback class corresponds
        /// </summary>
        private Engine parentEngine;
        /// <summary>
        /// The count of objects on the free list which triggers a deletion
        /// </summary>
        private const int freeListThreshold = 10;
        #endregion

    }
}
