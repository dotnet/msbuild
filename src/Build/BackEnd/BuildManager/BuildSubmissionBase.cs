// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    public abstract class BuildSubmissionBase
    {
        /// <summary>
        /// The completion event.
        /// </summary>
        protected readonly ManualResetEvent CompletionEvent;

        /// <summary>
        /// Flag indicating if logging is done.
        /// </summary>
        internal bool LoggingCompleted { get; private set; }

        /// <summary>
        /// True if it has been invoked
        /// </summary>
        protected int CompletionInvoked;

        //
        // Unfortunately covariant overrides are not available for .NET 472,
        //  so we have to use two set of properties for derived classes.
        internal abstract BuildRequestDataBase BuildRequestDataBase { get; }

        internal abstract BuildResultBase? BuildResultBase { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        protected internal BuildSubmissionBase(BuildManager buildManager, int submissionId)
        {
            ErrorUtilities.VerifyThrowArgumentNull(buildManager);

            BuildManager = buildManager;
            SubmissionId = submissionId;
            CompletionEvent = new ManualResetEvent(false);
            LoggingCompleted = false;
            CompletionInvoked = 0;
            BuildEventContext = Framework.BuildEventContext.CreateInitial(submissionId, Scheduler.VirtualNode);
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
        /// The build event context for this submission. This will have the submission ID set, and a nodeId of the scheduler's virtual node..
        /// </summary>
        public Framework.BuildEventContext BuildEventContext { get; }

        /// <summary>
        /// The asynchronous context provided to <see cref="BuildSubmission.ExecuteAsync(BuildSubmissionCompleteCallback, object)"/>, if any.
        /// </summary>
        public object? AsyncContext { get; protected set; }

        /// <summary>
        /// A <see cref="System.Threading.WaitHandle"/> which will be signalled when the build is complete.  Valid after <see cref="BuildSubmissionBase{TRequestData,TResultData}.Execute()"/> or <see cref="BuildSubmission.ExecuteAsync(BuildSubmissionCompleteCallback, object)"/> returns, otherwise null.
        /// </summary>
        public WaitHandle WaitHandle => CompletionEvent;

        /// <summary>
        /// Returns true if this submission is complete.
        /// </summary>
        public bool IsCompleted => WaitHandle.WaitOne(new TimeSpan(0));

        /// <summary>
        /// Whether the build has started.
        /// </summary>
        internal abstract bool IsStarted { get; set; }

        /// <summary>
        /// Indicates that all logging events for this submission are complete.
        /// </summary>
        internal void CompleteLogging()
        {
            LoggingCompleted = true;
            CheckForCompletion();
        }

        protected internal virtual void OnCompletition() { }
        protected internal abstract void CheckForCompletion();

        internal abstract BuildResultBase CompleteResultsWithException(Exception exception);
    }
}
