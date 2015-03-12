//-----------------------------------------------------------------------
// <copyright file="BuildSubmissionTestExtension.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension for the BuildSubmission implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Delegate which is called when the submission is completed.
    /// </summary>
    /// <param name="submissionTestExtension">BuildSubmissionTestExtension returned from the internal callback.</param>
    public delegate void SubmissionCompletedCallback(BuildSubmissionTestExtension submissionTestExtension);

    /// <summary>
    /// Test extension for BuildSubmission implementation.
    /// </summary>
    public class BuildSubmissionTestExtension : TestExtension<BuildSubmissionVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the BuildSubmissionTestExtension class.
        /// </summary>
        /// <param name="buildSubmission">BuildSubmission returned to the internal BuildManager.</param>
        internal BuildSubmissionTestExtension(BuildSubmission buildSubmission)
            : base()
        {
            this.BuildSubmission = buildSubmission;
        }

        /// <summary>
        /// Gets the results test extension of the build. This is valid only after the build is completed.
        /// </summary>
        public BuildResultTestExtension BuildResultTestExtension
        {
            get
            {
                BuildResult result = this.BuildSubmission.BuildResult;
                return TestExtensionHelper.Create<BuildResultTestExtension, BuildResult>(result, this);
            }
        }

        /// <summary>
        /// Gets the configuration id of the build request encapsulated by this submission.
        /// </summary>
        public int ConfigurationIdForSubmission
        {
            get
            {
                return this.BuildSubmission.BuildRequest.ConfigurationId;
            }
        }

        /// <summary>
        /// Gets BuildSubmission type which is returned by the BuildManager.
        /// </summary>
        internal BuildSubmission BuildSubmission
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets a callback method which is to be called when the submission completes.
        /// </summary>
        internal SubmissionCompletedCallback SubmissionCompletedCallback
        {
            get;
            set;
        }

        /// <summary>
        /// Starts the request and blocks until results are available.
        /// </summary>
        /// <returns>BuildResultTestExtension of the submitted build request. Returns only after the build is completed.</returns>
        public BuildResultTestExtension Execute()
        {
            BuildResult result = this.BuildSubmission.Execute();
            return TestExtensionHelper.Create<BuildResultTestExtension, BuildResult>(result, this);
        }

        /// <summary>
        /// Starts the request asynchronously and immediately returns control to the caller.
        /// </summary>
        /// <param name="callback">Method to call back when the submission is completed.</param>
        /// <param name="context">The context of the submission.</param>
        public void ExecuteAsync(SubmissionCompletedCallback callback, object context)
        {
            this.SubmissionCompletedCallback = callback;
            this.BuildSubmission.ExecuteAsync(this.BuildSubmissionCompleteCallback, context);
        }

        /// <summary>
        /// Callback method when the internal asynchronous BuildSubmission is completed. This basically
        /// creates a new BuildSubmissionTestExtension and calls the clients callback.
        /// </summary>
        /// <param name="buildSubmission">BuildSubmission record for which the build was completed.</param>
        internal void BuildSubmissionCompleteCallback(BuildSubmission buildSubmission)
        {
            BuildSubmissionTestExtension submissionTestExtension = new BuildSubmissionTestExtension(buildSubmission);
            this.SubmissionCompletedCallback(submissionTestExtension);
        }
    }
}
