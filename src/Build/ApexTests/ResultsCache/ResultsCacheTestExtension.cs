//-----------------------------------------------------------------------
// <copyright file="ResultsCacheTestExtension.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension for the ResultsCache implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test extension for ResultsCache implementation.
    /// </summary>
    public class ResultsCacheTestExtension : TestExtension<ResultsCacheVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the ResultsCacheTestExtension class.
        /// </summary>
        /// <param name="resultsCache">Instance of the result cache created by the build manager.</param>
        internal ResultsCacheTestExtension(ResultsCache resultsCache)
            : base()
        {
            this.ResultsCache = resultsCache;
        }

        /// <summary>
        /// Gets the BuildResult object returned by the BuildManager.
        /// </summary>
        internal ResultsCache ResultsCache
        {
            get;
            private set;
        }

        /// <summary>
        /// BuildResult for a specified configuration id.
        /// </summary>
        /// <param name="configurationId">Configuration id.</param>
        /// <returns>BuildResultTestExtension for the configuration id.</returns>
        public BuildResultTestExtension GetResultFromCache(int configurationId)
        {
            BuildResult result = this.ResultsCache.GetResultsForConfiguration(configurationId);
            return TestExtensionHelper.Create<BuildResultTestExtension, BuildResult>(result, this);
        }
    }
}
