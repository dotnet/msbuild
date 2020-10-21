// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Execution;
using NodeLoggingContext = Microsoft.Build.BackEnd.Logging.NodeLoggingContext;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Delegate for event raised when a new build request needs to be issued.
    /// </summary>
    /// <param name="issuingEntry">The entry issuing the request.</param>
    /// <param name="requests">The request to be issued.</param>
    internal delegate void NewBuildRequestsDelegate(BuildRequestEntry issuingEntry, FullyQualifiedBuildRequest[] requests);

    /// <summary>
    /// Delegate for event raised when a build request has completed.
    /// </summary>
    /// <param name="completedEntry">The entry which completed.</param>
    internal delegate void BuildRequestCompletedDelegate(BuildRequestEntry completedEntry);

    /// <summary>
    /// Delegate for event raised when a build request is blocked on another request which is in progress.
    /// </summary>
    /// <param name="issuingEntry">The build request entry which is being blocked.</param>
    /// <param name="blockingGlobalRequestId">The request on which we are blocked.</param>
    /// <param name="blockingTarget">The target on which we are blocked.</param>
    /// <param name="partialBuildResult">The partial build result on which we are blocked.</param>
    internal delegate void BuildRequestBlockedDelegate(BuildRequestEntry issuingEntry, int blockingGlobalRequestId, string blockingTarget, BuildResult partialBuildResult);

    /// <summary>
    /// Represents a class which is capable of building BuildRequestEntries.
    /// </summary>
    internal interface IRequestBuilder
    {
        /// <summary>
        /// Raised when a new build request is to be issued.
        /// </summary>
        event NewBuildRequestsDelegate OnNewBuildRequests;

        /// <summary>
        /// Raised when the build request is complete.
        /// </summary>
        event BuildRequestCompletedDelegate OnBuildRequestCompleted;

        /// <summary>
        /// Raised when a build request is blocked on another one in progress.
        /// </summary>
        event BuildRequestBlockedDelegate OnBuildRequestBlocked;

        /// <summary>
        /// Builds the request contained in the specified entry.
        /// </summary>
        /// <param name="nodeLoggingContext">The logging context for the node.</param>
        /// <param name="entry">The entry to be built.</param>
        void BuildRequest(NodeLoggingContext nodeLoggingContext, BuildRequestEntry entry);

        /// <summary>
        /// Continues building a request which was previously waiting for results.
        /// </summary>
        void ContinueRequest();

        /// <summary>
        /// Cancels an existing request.
        /// </summary>
        void CancelRequest();

        /// <summary>
        /// Starts to cancel an existing request.
        /// </summary>
        /// <remarks>
        /// This method should return immediately after signal the cancel event.
        /// "CancelRequest()" is equal to call "BeginCancel()" and "WaitForCancelCompletion()".
        /// We break "CancelRequest()" to 2 phases, so that we could signal cancel event
        /// to a bunch of requests without waiting, in order to optimize the "cancel build" scenario.
        /// </remarks>
        void BeginCancel();

        /// <summary>
        /// Waits for the cancellation until it's completed, and cleans up the internal states.
        /// </summary>
        void WaitForCancelCompletion();
    }
}
