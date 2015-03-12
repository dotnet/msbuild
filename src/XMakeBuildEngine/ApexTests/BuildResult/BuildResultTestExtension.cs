//-----------------------------------------------------------------------
// <copyright file="BuildResultTestExtension.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension for the BuildResult implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test extension for BuildResult implementation.
    /// </summary>
    public class BuildResultTestExtension : TestExtension<BuildResultVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the BuildResultTestExtension class.
        /// </summary>
        /// <param name="buildResult">Instance of the result returned by the internal BuildManager.</param>
        internal BuildResultTestExtension(BuildResult buildResult)
            : base()
        {
            this.BuildResult = buildResult;
        }

        /// <summary>
        /// Gets the BuildResult object returned by the BuildManager.
        /// </summary>
        internal BuildResult BuildResult
        {
            get;
            private set;
        }
    }
}
