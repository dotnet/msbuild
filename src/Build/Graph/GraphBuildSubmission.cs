// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Threading;
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
    public class GraphBuildSubmission
    {
        /// <summary>
        /// The callback to invoke when the submission is complete.
        /// </summary>
        private GraphBuildSubmissionCompleteCallback _completionCallback;

        /// <summary>
        /// The completion event.
        /// </summary>
        private readonly ManualResetEvent _completionEvent;

        /// <summary>
        /// True if it has been invoked
        /// </summary>
        private int _completionInvoked;

        /// <summary>
        /// Constructor
        /// </summary>
        internal GraphBuildSubmission(BuildManager buildManager, int submissionId, GraphBuildRequestData requestData)
        {
            ErrorUtilities.VerifyThrowArgumentNull(buildManager, nameof(buildManager));
            ErrorUtilities.VerifyThrowArgumentNull(requestData, nameof(requestData));

            BuildManager = buildManager;
            SubmissionId = submissionId;
            BuildRequestData = requestData;
            _completionEvent = new ManualResetEvent(false);
            _completionInvoked = 0;
        }

        /// <summary>
        /// The BuildManager with which this submission is associated.
        /// </summary>
        public BuildManager BuildManager { get; }

        /// <summary>
        /// An ID uniquely identifying this request from among other submissions within the same build.
        /// </summary>
        public int SubmissionId { get; }

        /// <summary>
        /// The asynchronous context provided to <see cref="BuildSubmission.ExecuteAsync(BuildSubmissionCompleteCallback, object)"/>, if any.
        /// </summary>
        public Object AsyncContext { get; private set; }

        /// <summary>
        /// A <see cref="System.Threading.WaitHandle"/> which will be signalled when the build is complete.  Valid after <see cref="BuildSubmission.Execute()"/> or <see cref="BuildSubmission.ExecuteAsync(BuildSubmissionCompleteCallback, object)"/> returns, otherwise null.
        /// </summary>
        public WaitHandle WaitHandle => _completionEvent;

        /// <summary>
        /// Returns true if this submission is complete.
        /// </summary>
        public bool IsCompleted => WaitHandle.WaitOne(new TimeSpan(0));

        /// <summary>
        /// The results of the build per graph node.  Valid only after WaitHandle has become signalled.
        /// </summary>
        public GraphBuildResult BuildResult { get; internal set; }

        /// <summary>
        /// The BuildRequestData being used for this submission.
        /// </summary>
        internal GraphBuildRequestData BuildRequestData { get; }

        /// <summary>
        /// Whether the graph build has started.
        /// </summary>
        internal bool IsStarted { get; set; }

        /// <summary>
        /// Starts the request and blocks until results are available.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public GraphBuildResult Execute()
        {
            ExecuteAsync(null, null);
            WaitHandle.WaitOne();

            return BuildResult;
        }

        /// <summary>
        /// Starts the request asynchronously and immediately returns control to the caller.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public void ExecuteAsync(GraphBuildSubmissionCompleteCallback callback, object context)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(!IsCompleted, "SubmissionAlreadyComplete");
            _completionCallback = callback;
            AsyncContext = context;
            BuildManager.ExecuteSubmission(this);
        }

        /// <summary>
        /// Sets the event signaling that the build is complete.
        /// </summary>
        internal void CompleteResults(GraphBuildResult result)
        {
            ErrorUtilities.VerifyThrowArgumentNull(result, nameof(result));
            ErrorUtilities.VerifyThrow(result.SubmissionId == SubmissionId, "GraphBuildResult's submission id doesn't match GraphBuildSubmission's");

            bool hasCompleted = (Interlocked.Exchange(ref _completionInvoked, 1) == 1);
            if (!hasCompleted)
            {
                BuildResult = result;
                _completionEvent.Set();

                if (_completionCallback != null)
                {
                    void Callback(object state)
                    {
                        _completionCallback(this);
                    }

                    ThreadPoolExtensions.QueueThreadPoolWorkItemWithCulture(Callback, CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
                }
            }
        }
    }
}
