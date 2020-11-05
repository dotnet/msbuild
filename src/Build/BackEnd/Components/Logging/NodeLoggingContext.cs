// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// The logging context for an entire node.
    /// </summary>
    internal class NodeLoggingContext : BuildLoggingContext
    {
        /// <summary>
        /// Used to create the initial, base logging context for the node.
        /// </summary>
        /// <param name="loggingService">The logging service to use.</param>
        /// <param name="nodeId">The </param>
        /// <param name="inProcNode"><code>true</code> if this is an in-process node, otherwise <code>false</code>.</param>
        internal NodeLoggingContext(ILoggingService loggingService, int nodeId, bool inProcNode)
            : base(loggingService, new BuildEventContext(nodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId), inProcNode)
        {
            ErrorUtilities.VerifyThrow(nodeId != BuildEventContext.InvalidNodeId, "Should not ever be given an invalid NodeId");

            // The in-proc node will have its BuildStarted, BuildFinished events sent by the BuildManager itself.
            if (!IsInProcNode)
            {
                LoggingService.LogBuildStarted();
            }

            this.IsValid = true;
        }

        /// <summary>
        /// Log the completion of a build
        /// </summary>
        /// <param name="success">Did the build succeed or not</param>
        internal void LogBuildFinished(bool success)
        {
            ErrorUtilities.VerifyThrow(this.IsValid, "Build not started.");

            // The in-proc node will have its BuildStarted, BuildFinished events sent by the BuildManager itself.
            if (!IsInProcNode)
            {
                LoggingService.LogBuildFinished(success);
            }

            this.IsValid = false;
        }

        /// <summary>
        /// Log that a project has started if it has no parent (the first project)
        /// </summary>
        /// <param name="requestEntry">The build request entry for this project.</param>
        /// <returns>The BuildEventContext to use for this project.</returns>
        internal ProjectLoggingContext LogProjectStarted(BuildRequestEntry requestEntry)
        {
            ErrorUtilities.VerifyThrow(this.IsValid, "Build not started.");
            return new ProjectLoggingContext(this, requestEntry, requestEntry.Request.ParentBuildEventContext);
        }

        /// <summary>
        /// Log that a project has started if it is serviced from the cache
        /// </summary>
        /// <param name="request">The build request.</param>
        /// <param name="configuration">The configuration used to build the request.</param>
        /// <returns>The BuildEventContext to use for this project.</returns>
        internal ProjectLoggingContext LogProjectStarted(BuildRequest request, BuildRequestConfiguration configuration)
        {
            ErrorUtilities.VerifyThrow(this.IsValid, "Build not started.");
            return new ProjectLoggingContext(this, request, configuration.ProjectFullPath, configuration.ToolsVersion, request.ParentBuildEventContext);
        }

        /// <summary>
        /// Logs the project started/finished pair for projects which are skipped entirely because all
        /// of their results are available in the cache.
        /// </summary>
        internal void LogRequestHandledFromCache(BuildRequest request, BuildRequestConfiguration configuration, BuildResult result)
        {
            ProjectLoggingContext projectLoggingContext = LogProjectStarted(request, configuration);

            // When pulling a request from the cache, we want to make sure we log a task skipped message for any targets which 
            // were used to build the request including default and inital targets.
            foreach (string target in configuration.GetTargetsUsedToBuildRequest(request))
            {
                projectLoggingContext.LogComment
                    (
                        MessageImportance.Low,
                        result[target].ResultCode == TargetResultCode.Failure ? "TargetAlreadyCompleteFailure" : "TargetAlreadyCompleteSuccess",
                        target
                    );

                if (result[target].ResultCode == TargetResultCode.Failure)
                {
                    break;
                }
            }

            projectLoggingContext.LogProjectFinished(result.OverallResult == BuildResultCode.Success);
        }
    }
}
