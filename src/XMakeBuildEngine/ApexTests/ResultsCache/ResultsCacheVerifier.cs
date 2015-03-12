//-----------------------------------------------------------------------
// <copyright file="ResultsCacheVerifier.cs" company="Microsoft">
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
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test verifier for BuildResult implementation.
    /// </summary>
    public class ResultsCacheVerifier : TestExtensionVerifier<ResultsCacheTestExtension>
    {
        /// <summary>
        /// Gets Test extension associated with this verifier.
        /// </summary>
        internal new ResultsCacheTestExtension TestExtension
        {
            get
            {
                return base.TestExtension as ResultsCacheTestExtension;
            }
        }
    }
}
