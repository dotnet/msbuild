// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// A callback used to receive notification that a build has completed.
    /// </summary>
    /// <remarks>
    /// When this delegate is invoked, the WaitHandle on the BuildSubmission will have been be signalled and the OverallBuildResult will be valid.
    /// </remarks>
    public delegate void GraphBuildSubmissionCompleteCallback(GraphBuildSubmission submission);

    /// <summary>
    /// A GraphBuildSubmission represents a graph build request which has been submitted to the BuildManager for processing.  It may be used to
    /// execute synchronous or asynchronous graph build requests and provides access to the results upon completion.
    /// </summary>
    /// <remarks>
    /// This class is thread-safe.
    /// </remarks>
    public class GraphBuildSubmission : BuildSubmissionBase<GraphBuildRequestData, GraphBuildResult>
    {
        internal GraphBuildSubmission(BuildManager buildManager, int submissionId, GraphBuildRequestData requestData) :
            base(buildManager, submissionId, requestData)
        {
            CompleteLogging();
        }

        /// <summary>
        /// Starts the request asynchronously and immediately returns control to the caller.
        /// </summary>
        /// <exception cref="InvalidOperationException">The request has already been started or is already complete.</exception>
        public void ExecuteAsync(GraphBuildSubmissionCompleteCallback? callback, object? context)
        {
            void Clb(BuildSubmissionBase<GraphBuildRequestData, GraphBuildResult> submission)
            {
                callback?.Invoke((GraphBuildSubmission)submission);
            }

            ExecuteAsync(Clb, context, allowMainThreadBuild: false);
        }

        /// <summary>
        /// Starts the request and blocks until results are available.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public override GraphBuildResult Execute()
        {
            ExecuteAsync(null, null);
            WaitHandle.WaitOne();

            ErrorUtilities.VerifyThrow(BuildResult != null,
                "BuildResult is not populated after Execute is done.");

            return BuildResult!;
        }

        protected internal override void CheckResultValidForCompletion(GraphBuildResult result)
        {
            ErrorUtilities.VerifyThrow(result.SubmissionId == SubmissionId,
                "GraphBuildResult's submission id doesn't match GraphBuildSubmission's");
        }

        protected internal override GraphBuildResult CreateFailedResult(Exception exception)
            => new(SubmissionId, exception);
    }
}
