// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using BuildResult = Microsoft.Build.Execution.BuildResult;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This interface represents an object which holds build results.
    /// </summary>
    internal interface IResultsCache : IBuildComponent
    {
        /// <summary>
        /// Adds a result to the cache
        /// </summary>
        /// <param name="result">The result to add.</param>
        void AddResult(BuildResult result);

        /// <summary>
        /// Deletes all results from the cache for the specified build.
        /// </summary>
        void ClearResults();

        /// <summary>
        /// Retrieves a BuildResult for the specified matching BuildRequest.
        /// </summary>
        /// <param name="request">The request for which the result should be returned.</param>
        /// <returns>A BuildResult if there is a matching one in the cache, otherwise null.</returns>
        BuildResult GetResultForRequest(BuildRequest request);

        /// <summary>
        /// Retrieves a BuildResult for the specified configuration.
        /// </summary>
        /// <param name="configurationId">The configuration for which results should be returned.</param>
        /// <returns>A BuildResult if there is a matching one in the cache, otherwise null.</returns>
        BuildResult GetResultsForConfiguration(int configurationId);

        /// <summary>
        /// Attempts to satisfy the request from the cache.  The request can be satisfied only if:
        /// 1. All specified targets in the request have non-skipped results in the cache.
        /// 2. All initial targets in the configuration for the request have non-skipped results in the cache.
        /// 3. If there are no specified targets, then all default targets in the request must have non-skipped results
        ///    in the cache.
        /// </summary>
        /// <param name="request">The request whose results we should return</param>
        /// <param name="configInitialTargets">The initial targets for the request's configuration.</param>
        /// <param name="configDefaultTargets">The default targets for the request's configuration.</param>
        /// <param name="additionalTargetsToCheckForOverallResult">Any additional targets that need to be checked to determine overall 
        /// pass or failure, but that are not included as actual results. (E.g. AfterTargets of an entrypoint target)</param>
        /// <param name="skippedResultsAreOK">If false, a cached skipped target will cause this method to return "NotSatisfied".  
        /// If true, then as long as there is a result in the cache (regardless of whether it was skipped or not), this method 
        /// will return "Satisfied". In most cases this should be false, but it may be set to true in a situation where there is no 
        /// chance of re-execution (which is the usual response to missing / skipped targets), and the caller just needs the data.</param>
        /// <returns>A response indicating the results, if any, and the targets needing to be built, if any.</returns>
        ResultsCacheResponse SatisfyRequest(BuildRequest request, List<string> configInitialTargets, List<string> configDefaultTargets, List<string> additionalTargetsToCheckForOverallResult, bool skippedResultsAreOK);

        /// <summary>
        /// Clears the results for a specific configuration.
        /// </summary>
        /// <param name="configurationId">The configuration id.</param>
        void ClearResultsForConfiguration(int configurationId);

        /// <summary>
        /// Caches results to disk if possible.
        /// </summary>
        void WriteResultsToDisk();
    }
}
