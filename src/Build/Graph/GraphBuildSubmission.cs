// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Graph
{
    public class GraphBuildSubmission : BuildSubmission<GraphBuildRequestData, GraphBuildResult>
    {
        internal GraphBuildSubmission(BuildManager buildManager, int submissionId, GraphBuildRequestData requestData) :
            base(buildManager, submissionId, requestData)
        {
            CompleteLogging();
        }

        /// <summary>
        /// Starts the request and blocks until results are available.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public override GraphBuildResult Execute()
        {
            ExecuteAsync(null, null);
            WaitHandle.WaitOne();

            return BuildResult;
        }
    }

    /// <summary>
    /// A callback used to receive notification that a build has completed.
    /// </summary>
    /// <remarks>
    /// When this delegate is invoked, the WaitHandle on the BuildSubmission will have been be signalled and the OverallBuildResult will be valid.
    /// </remarks>
    public delegate void GraphBuildSubmissionCompleteCallback(GraphBuildSubmission submission);
}
