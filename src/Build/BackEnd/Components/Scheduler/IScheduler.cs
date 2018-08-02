// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Represents an object which provides scheduling services for BuildRequests over Nodes.
    /// </summary>
    internal interface IScheduler : IBuildComponent
    {
        /// <summary>
        /// Retrieves the minimum assignable configuration id
        /// </summary>
        int MinimumAssignableConfigurationId { get; }

        /// <summary>
        /// Determines if the specified configuration is currently being built
        /// </summary>
        /// <param name="configurationId">The configuration to query for</param>
        /// <returns>True if the configuration is being built somewhere, false otherwise.</returns>
        bool IsCurrentlyBuildingConfiguration(int configurationId);

        /// <summary>
        /// Retrieves a configuration id for a configuration which has a matching path
        /// </summary>
        /// <param name="configurationPath">The path for the configuration</param>
        /// <returns>A positive configuration id if one exists in the plan, 0 otherwise.</returns>
        int GetConfigurationIdFromPlan(string configurationPath);

        /// <summary>
        /// Reports to the scheduler that a request is blocked.
        /// </summary>
        /// <param name="nodeId">The node making the report.</param>
        /// <param name="blocker">The thing blocking the active request on the node.</param>
        /// <returns>Action to be taken.</returns>
        IEnumerable<ScheduleResponse> ReportRequestBlocked(int nodeId, BuildRequestBlocker blocker);

        /// <summary>
        /// Reports to the scheduler that a new result has been generated for a build request.
        /// </summary>
        /// <param name="nodeId">The node reporting the request.</param>
        /// <param name="result">The result.</param>
        /// <returns>Action to be taken.</returns>
        IEnumerable<ScheduleResponse> ReportResult(int nodeId, BuildResult result);

        /// <summary>
        /// Reports to the scheduler that a node has been created.
        /// </summary>
        /// <param name="nodeInfo">Info about the created nodes.</param>
        /// <returns>Action to be taken.</returns>
        IEnumerable<ScheduleResponse> ReportNodesCreated(IEnumerable<NodeInfo> nodeInfo);

        /// <summary>
        /// Reports to the scheduler than a node aborted the build.
        /// </summary>
        /// <param name="nodeId">The node which aborted.</param>
        void ReportBuildAborted(int nodeId);

        /// <summary>
        /// Resets the scheduler.
        /// </summary>
        void Reset();

        /// <summary>
        /// Writes a detailed summary of the build state which includes informaiton about the scheduling plan.
        /// </summary>
        void WriteDetailedSummary(int submissionId);
    }
}
