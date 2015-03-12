//-----------------------------------------------------------------------
// <copyright file="BuildSubmissionVerifier.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension verifier for the BuildSubmission implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test extension verifier for the BuildSubmission implementation.
    /// </summary>
    public class BuildSubmissionVerifier : TestExtensionVerifier<BuildSubmissionTestExtension>
    {
        /// <summary>
        /// Gets Test extension associated with this verifier.
        /// </summary>
        internal new BuildSubmissionTestExtension TestExtension
        {
            get
            {
                return base.TestExtension as BuildSubmissionTestExtension;
            }
        }

        /// <summary>
        /// Verifies if the BuildSubmission was completed.
        /// </summary>
        public void BuildIsCompleted()
        {
            this.Verifier.IsTrue(this.TestExtension.BuildSubmission.IsCompleted, "BuildSubmission should have been completed.");
        }

        /// <summary>
        /// Verifies if the BuildSubmission is pending.
        /// </summary>
        public void BuildIsRunning()
        {
            this.Verifier.IsTrue(!this.TestExtension.BuildSubmission.IsCompleted, "BuildSubmission should have been not completed or executing.");
        }

        /// <summary>
        /// Verify that build completed and the result is a success.
        /// </summary>
        public void BuildCompletedSuccessfully()
        {
            this.BuildIsCompleted();
            this.TestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
        }

        /// <summary>
        /// Verify that build completed and the result is a Failure.
        /// </summary>
        public void BuildCompletedButFailed()
        {
            this.BuildIsCompleted();
            this.TestExtension.BuildResultTestExtension.Verify.BuildFailed();
        }
    }
}