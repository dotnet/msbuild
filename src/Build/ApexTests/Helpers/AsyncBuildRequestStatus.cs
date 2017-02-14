//-----------------------------------------------------------------------
// <copyright file="AsyncBuildRequestStatus.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Class which has information about an execution of a build request.</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Collections;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;

    /// <summary>
    /// Class containing information about a BuildRequest which was executed asynchronously.
    /// </summary>
    public class AsyncBuildRequestStatus
    {
        /// <summary>
        /// Initializes a new instance of the AsyncBuildRequestStatus class.
        /// </summary>
        /// <param name="submissionCompletedEvent">Event handler which is to be set when the callback is called.</param>
        /// <param name="submissionTestExtension">Test extension for the submission.</param>
        public AsyncBuildRequestStatus(AutoResetEvent submissionCompletedEvent, BuildSubmissionTestExtension submissionTestExtension)
        {
            this.SubmissionCompletedEvent = submissionCompletedEvent;
            this.SubmissionTestExtension = submissionTestExtension;
            this.SubmissionsAreSame = false;
        }

        /// <summary>
        /// Gets the event to signal when the submission callback has been called.
        /// </summary>
        public AutoResetEvent SubmissionCompletedEvent
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the submission testextension passed when creating this class.
        /// </summary>
        public BuildSubmissionTestExtension SubmissionTestExtension
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the submission testextension passed by MSBuild during the callback.
        /// </summary>
        public BuildSubmissionTestExtension SubmissionTestExtensionFromClassBack
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the value indicating whether the submission  which was used to create this callback is the same as the one received in the callback.
        /// </summary>
        public bool SubmissionsAreSame
        {
            get;
            private set;
        }

        /// <summary>
        /// Callback method when the asynchronous BuildSubmission is completed. The verification done on completed submission is
        /// that the build completed and succeeded. This is the default behavior. If the verification is to be different then SubmissionCompletedVerificationType
        /// has to be used.
        /// </summary>
        /// <param name="submissionTestExtension">Contains the BuildSubmission for which the request was completed.</param>
        public void SubmissionCompletedCallback(BuildSubmissionTestExtension submissionTestExtension)
        {
            if (this.SubmissionTestExtension.BuildSubmission.BuildRequest.ConfigurationId == submissionTestExtension.BuildSubmission.BuildRequest.ConfigurationId)
            {
                this.SubmissionsAreSame = true;
            }

            this.SubmissionTestExtensionFromClassBack = submissionTestExtension;
            this.SubmissionCompletedEvent.Set();
        }
    }
}