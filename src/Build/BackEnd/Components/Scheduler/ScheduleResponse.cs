// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The type of action to take in response to a scheduling request.
    /// </summary>
    internal enum ScheduleActionType
    {
        /// <summary>
        /// The response indicates that no action should be taken.
        /// </summary>
        NoAction,

        /// <summary>
        /// The response indicates that the request should be sent to the specified node.
        /// </summary>
        Schedule,

        /// <summary>
        /// The response indicates that the request should be send to the specified node, 
        /// along with the configuration for the request.
        /// </summary>
        ScheduleWithConfiguration,

        /// <summary>
        /// The response has results for a particular blocked request
        /// </summary>
        ReportResults,

        /// <summary>
        /// The specified request id should now resume execution
        /// </summary>
        ResumeExecution,

        /// <summary>
        /// The response indicates that a new node should be created rather than scheduling this request.
        /// The request may be scheduled at a later time.
        /// </summary>
        CreateNode,

        /// <summary>
        /// The response indicates that the submission is complete.
        /// </summary>
        SubmissionComplete,

        /// <summary>
        /// The last action caused a circular dependency which cannot be resolved.
        /// </summary>
        CircularDependency
    }

    /// <summary>
    /// A response from the scheduler indicating where a build request should be handled.
    /// </summary>
    internal class ScheduleResponse
    {
        /// <summary>
        /// The type of action to take on this response.
        /// </summary>
        internal readonly ScheduleActionType Action;

        /// <summary>
        /// The node ID to which the request should be sent.
        /// </summary>
        internal readonly int NodeId;

        /// <summary>
        /// The results for a completed submission.
        /// </summary>
        internal readonly BuildResult BuildResult;

        /// <summary>
        /// The build request to send.
        /// </summary>
        internal readonly BuildRequest BuildRequest;

        /// <summary>
        /// The unblocking information.
        /// </summary>
        internal readonly BuildRequestUnblocker Unblocker;

        /// <summary>
        /// The type of node we must create.
        /// </summary>
        internal readonly NodeAffinity RequiredNodeType;

        /// <summary>
        /// The number of nodes of the requested affinity to create.
        /// </summary>
        internal readonly int NumberOfNodesToCreate;

        /// <summary>
        /// Constructs a response where no action should be taken.
        /// </summary>
        internal ScheduleResponse(ScheduleActionType type)
        {
            Action = type;
        }

        /// <summary>
        /// Constructs a response indicating what type of node we need to create.
        /// </summary>
        private ScheduleResponse(NodeAffinity affinity, int count)
        {
            Action = ScheduleActionType.CreateNode;
            RequiredNodeType = affinity;
            NumberOfNodesToCreate = count;
        }

        /// <summary>
        /// Constructs a response indicating that a specific submission has completed.
        /// </summary>
        private ScheduleResponse(BuildResult result)
        {
            Action = ScheduleActionType.SubmissionComplete;
            BuildResult = result;
        }

        /// <summary>
        /// Constructs a response indicating there is a circular dependency caused by the specified request.
        /// </summary>
        private ScheduleResponse(int nodeId, BuildRequest parentRequest, BuildRequest requestCausingCircularDependency)
        {
            Action = ScheduleActionType.CircularDependency;
            BuildRequest = requestCausingCircularDependency;
            NodeId = nodeId;
            Unblocker = new BuildRequestUnblocker(parentRequest, new BuildResult(requestCausingCircularDependency, true /* circularDependency */));
        }

        /// <summary>
        /// Constructs a response where a request should be scheduled.
        /// </summary>
        /// <param name="node">The node ID to which the request should be sent.</param>
        /// <param name="request">The request to send.</param>
        /// <param name="sendConfiguration"><code>true</code> to send the configuration, otherwise <code>false</code>.</param>
        private ScheduleResponse(int node, BuildRequest request, bool sendConfiguration)
        {
            Action = sendConfiguration ? ScheduleActionType.ScheduleWithConfiguration : ScheduleActionType.Schedule;
            NodeId = node;
            BuildRequest = request;
        }

        /// <summary>
        /// Constructs a response where a result should be sent or execution should be resumed.
        /// </summary>
        /// <param name="node">The node ID to which the result should be sent.</param>
        /// <param name="unblocker">The result to send.</param>
        private ScheduleResponse(int node, BuildRequestUnblocker unblocker)
        {
            Action = (unblocker.Result == null) ? ScheduleActionType.ResumeExecution : ScheduleActionType.ReportResults;
            NodeId = node;
            Unblocker = unblocker;
            BuildResult = unblocker.Result;
        }

        /// <summary>
        /// Creates a Schedule or ScheduleWithConfiguration response
        /// </summary>
        /// <param name="node">The node to which the response should be sent.</param>
        /// <param name="requestToSchedule">The request to be scheduled.</param>
        /// <param name="sendConfiguration">Flag indicating whether or not the configuration for the request must be sent to the node as well.</param>
        /// <returns>The ScheduleResponse.</returns>
        public static ScheduleResponse CreateScheduleResponse(int node, BuildRequest requestToSchedule, bool sendConfiguration)
        {
            return new ScheduleResponse(node, requestToSchedule, sendConfiguration);
        }

        /// <summary>
        /// Creates a ReportResult response.
        /// </summary>
        /// <param name="node">The node to which the response should be sent.</param>
        /// <param name="resultToReport">The result to be reported.</param>
        /// <returns>The ScheduleResponse.</returns>
        public static ScheduleResponse CreateReportResultResponse(int node, BuildResult resultToReport)
        {
            return new ScheduleResponse(node, new BuildRequestUnblocker(resultToReport));
        }

        /// <summary>
        /// Creates a ResumeExecution response.
        /// </summary>
        /// <param name="node">The node to which the response should be sent.</param>
        /// <param name="globalRequestIdToResume">The request which should resume executing.</param>
        /// <returns>The ScheduleResponse.</returns>
        public static ScheduleResponse CreateResumeExecutionResponse(int node, int globalRequestIdToResume)
        {
            return new ScheduleResponse(node, new BuildRequestUnblocker(globalRequestIdToResume));
        }

        /// <summary>
        /// Creates a CircularDependency response.
        /// </summary>
        /// <param name="node">The node to which the response should be sent.</param>
        /// <param name="parentRequest">The request which attempted to invoke the request causing the circular dependency.</param>
        /// <param name="requestCausingCircularDependency">The request which caused the circular dependency.</param>
        /// <returns>The ScheduleResponse.</returns>
        public static ScheduleResponse CreateCircularDependencyResponse(int node, BuildRequest parentRequest, BuildRequest requestCausingCircularDependency)
        {
            return new ScheduleResponse(node, parentRequest, requestCausingCircularDependency);
        }

        /// <summary>
        /// Creates a SubmissionComplete response.
        /// </summary>
        /// <param name="rootRequestResult">The result for the submission's root request.</param>
        /// <returns>The ScheduleResponse.</returns>
        public static ScheduleResponse CreateSubmissionCompleteResponse(BuildResult rootRequestResult)
        {
            return new ScheduleResponse(rootRequestResult);
        }

        /// <summary>
        /// Create a CreateNode response
        /// </summary>
        /// <param name="typeOfNodeToCreate">The type of node to create.</param>
        /// <param name="count">The number of new nodes of that particular affinity to create.</param>
        /// <returns>The ScheduleResponse.</returns>
        public static ScheduleResponse CreateNewNodeResponse(NodeAffinity typeOfNodeToCreate, int count)
        {
            return new ScheduleResponse(typeOfNodeToCreate, count);
        }

        /// <summary>
        /// Returns the schedule response as a descriptive string.
        /// </summary>
        public override string ToString()
        {
            switch (Action)
            {
                case ScheduleActionType.ReportResults:
                case ScheduleActionType.ResumeExecution:
                    return String.Format(CultureInfo.CurrentCulture, "Act: {0} Node: {1} Request: {2}", Action, NodeId, Unblocker.BlockedRequestId);

                case ScheduleActionType.Schedule:
                    return String.Format(CultureInfo.CurrentCulture, "Act: {0} Node: {1} Request: {2} Parent {3}", Action, NodeId, BuildRequest.GlobalRequestId, BuildRequest.ParentGlobalRequestId);

                case ScheduleActionType.ScheduleWithConfiguration:
                    return String.Format(CultureInfo.CurrentCulture, "Act: {0} Node: {1} Request: {2} Parent {3} Configuration: {4}", Action, NodeId, BuildRequest.GlobalRequestId, BuildRequest.ParentGlobalRequestId, BuildRequest.ConfigurationId);

                case ScheduleActionType.CircularDependency:
                    return String.Format(CultureInfo.CurrentCulture, "Act: {0} Node: {1} Request: {2} Parent {3} Configuration: {4}", Action, NodeId, BuildRequest.GlobalRequestId, BuildRequest.ParentGlobalRequestId, BuildRequest.ConfigurationId);

                case ScheduleActionType.SubmissionComplete:
                    return String.Format(CultureInfo.CurrentCulture, "Act: {0} Submission: {1}", Action, BuildResult.SubmissionId);

                case ScheduleActionType.CreateNode:
                    return String.Format(CultureInfo.CurrentCulture, "Act: {0} Count: {1}", Action, NumberOfNodesToCreate);

                case ScheduleActionType.NoAction:
                default:
                    return String.Format(CultureInfo.CurrentCulture, "Act: {0}", Action);
            }
        }
    }
}
