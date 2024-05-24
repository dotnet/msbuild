// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// A callback used to receive notification that a build has completed.
    /// </summary>
    /// <remarks>
    /// When this delegate is invoked, the WaitHandle on the BuildSubmission will have been be signalled and the OverallBuildResult will be valid.
    /// </remarks>
    public delegate void BuildSubmissionCompleteCallback<TRequestData, TResultData>(
        BuildSubmission<TRequestData, TResultData> submission)
        where TRequestData : BuildRequestDataBase
        where TResultData : BuildResultBase;

    public abstract class BuildSubmissionBase { }

    public abstract class BuildSubmission<TRequestData, TResultData> : BuildSubmissionBase
        where TRequestData : BuildRequestDataBase
        where TResultData : BuildResultBase
    {
        /// <summary>
        /// The callback to invoke when the submission is complete.
        /// </summary>
        private BuildSubmissionCompleteCallback<TRequestData, TResultData> _completionCallback;

        /// <summary>
        /// The completion event.
        /// </summary>
        private readonly ManualResetEvent _completionEvent;

        /// <summary>
        /// Flag indicating if logging is done.
        /// </summary>
        internal bool LoggingCompleted { get; private set; }

        /// <summary>
        /// True if it has been invoked
        /// </summary>
        private int _completionInvoked;

        /// <summary>
        /// Constructor
        /// </summary>
        internal protected BuildSubmission(BuildManager buildManager, int submissionId, TRequestData requestData)
        {
            ErrorUtilities.VerifyThrowArgumentNull(buildManager, nameof(buildManager));
            ErrorUtilities.VerifyThrowArgumentNull(requestData, nameof(requestData));

            BuildManager = buildManager;
            SubmissionId = submissionId;
            BuildRequestData = requestData;
            _completionEvent = new ManualResetEvent(false);
            LoggingCompleted = false;
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
        /// The asynchronous context provided to <see cref="BuildSubmission{TRequestData,TResultData}.ExecuteAsync(BuildSubmissionCompleteCallback, object)"/>, if any.
        /// </summary>
        public Object AsyncContext { get; private set; }

        /// <summary>
        /// A <see cref="System.Threading.WaitHandle"/> which will be signalled when the build is complete.  Valid after <see cref="BuildSubmission{TRequestData,TResultData}.Execute()"/> or <see cref="BuildSubmission{TRequestData,TResultData}.ExecuteAsync(BuildSubmissionCompleteCallback, object)"/> returns, otherwise null.
        /// </summary>
        public WaitHandle WaitHandle => _completionEvent;

        /// <summary>
        /// Returns true if this submission is complete.
        /// </summary>
        public bool IsCompleted => WaitHandle.WaitOne(new TimeSpan(0));

        /// <summary>
        /// The results of the build per graph node.  Valid only after WaitHandle has become signalled.
        /// </summary>
        public TResultData BuildResult { get; internal set; }

        /// <summary>
        /// The BuildRequestData being used for this submission.
        /// </summary>
        internal TRequestData BuildRequestData { get; }

        /// <summary>
        /// Whether the graph build has started.
        /// </summary>
        internal bool IsStarted { get; set; }

        /// <summary>
        /// Starts the request and blocks until results are available.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public abstract TResultData Execute();

        /// <summary>
        /// Starts the request asynchronously and immediately returns control to the caller.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public void ExecuteAsync(BuildSubmissionCompleteCallback<TRequestData, TResultData> callback, object context)
        {
            ExecuteAsync(callback, context, allowMainThreadBuild: false);
        }

        protected void ExecuteAsync(
            BuildSubmissionCompleteCallback<TRequestData, TResultData> callback,
            object context,
            bool allowMainThreadBuild)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(!IsCompleted, "SubmissionAlreadyComplete");
            _completionCallback = callback;
            AsyncContext = context;
            BuildManager.ExecuteSubmission(this, allowMainThreadBuild);
        }

        /// <summary>
        /// Indicates that all logging events for this submission are complete.
        /// </summary>
        internal void CompleteLogging()
        {
            LoggingCompleted = true;
            CheckForCompletion();
        }

        /// <summary>
        /// Sets the event signaling that the build is complete.
        /// </summary>
        internal void CompleteResults(TResultData result)
        {
            ErrorUtilities.VerifyThrowArgumentNull(result, nameof(result));
            ErrorUtilities.VerifyThrow(result.SubmissionId == SubmissionId,
                "GraphBuildResult's submission id doesn't match GraphBuildSubmission's");

            BuildResult ??= result;

            CheckForCompletion();
        }

        protected internal virtual void OnCompletition() { }

        /// <summary>
        /// Determines if we are completely done with this submission and can complete it so the user may access results.
        /// </summary>
        private void CheckForCompletion()
        {
            if (BuildResult != null && LoggingCompleted)
            {
                bool hasCompleted = (Interlocked.Exchange(ref _completionInvoked, 1) == 1);
                if (!hasCompleted)
                {
                    OnCompletition();
                    ////// Did this submission have warnings elevated to errors? If so, mark it as
                    ////// failed even though it succeeded (with warnings--but they're errors).
                    ////if (((IBuildComponentHost)BuildManager).LoggingService.HasBuildSubmissionLoggedErrors(BuildResult.SubmissionId))
                    ////{
                    ////    BuildResult.SetOverallResult(overallResult: false);
                    ////}

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


    /// <summary>
    /// A callback used to receive notification that a build has completed.
    /// </summary>
    /// <remarks>
    /// When this delegate is invoked, the WaitHandle on the BuildSubmission will have been be signalled and the OverallBuildResult will be valid.
    /// </remarks>
    public delegate void BuildSubmissionCompleteCallback(BuildSubmission submission);

    public sealed class BuildSubmission : BuildSubmission<BuildRequestData, BuildResult>
    {
        /// <summary>
        /// Flag indicating whether synchronous wait should support legacy threading semantics.
        /// </summary>
        private readonly bool _legacyThreadingSemantics;

        /// <summary>
        /// The build request for execution.
        /// </summary>
        internal BuildRequest BuildRequest { get; set; }

        internal BuildSubmission(BuildManager buildManager, int submissionId, BuildRequestData requestData, bool legacyThreadingSemantics)
            : base(buildManager, submissionId, requestData)
        {
            _legacyThreadingSemantics = legacyThreadingSemantics;
        }

        /// <summary>
        /// Starts the request and blocks until results are available.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public override BuildResult Execute()
        {
            // TODO: here
            // ((IBuildComponentHost)BuildManager).LoggingService.LogBuildEvent()
            // BuildEventContext buildEventContext = new BuildEventContext(this.SubmissionId, 1, BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidTaskId);

            // async as well !!

            LegacyThreadingData legacyThreadingData = ((IBuildComponentHost)BuildManager).LegacyThreadingData;
            legacyThreadingData.RegisterSubmissionForLegacyThread(SubmissionId);

            ExecuteAsync(null, null, _legacyThreadingSemantics);
            if (_legacyThreadingSemantics)
            {
                RequestBuilder.WaitWithBuilderThreadStart(new[] { WaitHandle }, false, legacyThreadingData, SubmissionId);
            }
            else
            {
                WaitHandle.WaitOne();
            }

            legacyThreadingData.UnregisterSubmissionForLegacyThread(SubmissionId);

            return BuildResult;
        }

        protected internal override void OnCompletition()
        {
            // Did this submission have warnings elevated to errors? If so, mark it as
            // failed even though it succeeded (with warnings--but they're errors).
            if (((IBuildComponentHost)BuildManager).LoggingService.HasBuildSubmissionLoggedErrors(BuildResult.SubmissionId))
            {
                BuildResult.SetOverallResult(overallResult: false);
            }
        }
    }
}
