//-----------------------------------------------------------------------
// <copyright file="BuildResultVerifier.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension for the BuildResult implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Exceptions;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test verifier for BuildResult implementation.
    /// </summary>
    public class BuildResultVerifier : TestExtensionVerifier<BuildResultTestExtension>
    {
        /// <summary>
        /// Gets Test extension associated with this verifier.
        /// </summary>
        internal new BuildResultTestExtension TestExtension
        {
            get
            {
                return base.TestExtension as BuildResultTestExtension;
            }
        }

        /// <summary>
        /// Verify if the build succeeded.
        /// </summary>
        public void BuildSucceeded()
        {
            this.Verifier.IsTrue((this.TestExtension.BuildResult.OverallResult == BuildResultCode.Success), "OverallResult of the build was expected to be successful.");
        }

        /// <summary>
        /// Verify if the build failed.
        /// </summary>
        public void BuildFailed()
        {
            this.Verifier.IsTrue((this.TestExtension.BuildResult.OverallResult == BuildResultCode.Failure), "OverallResult of the build was expected to be Failure.");
        }

        /// <summary>
        /// Verify if the result contains BuildAborted exception due to a cancel.
        /// </summary>
        public void BuildWasAborted()
        {
            this.Verifier.IsTrue(this.TestExtension.BuildResult.Exception.GetType() == typeof(BuildAbortedException), "Result should contain BuildAborted exception.");
        }
    }
}
