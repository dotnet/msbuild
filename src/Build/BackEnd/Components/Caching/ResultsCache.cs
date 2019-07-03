// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Implementation of the results cache.
    /// </summary>
    internal class ResultsCache : IResultsCache
    {
        /// <summary>
        /// The table of all build results.  This table is indexed by configuration id and
        /// contains BuildResult objects which have all of the target information.
        /// </summary>
        private ConcurrentDictionary<int, BuildResult> _resultsByConfiguration;

        /// <summary>
        /// Creates an empty results cache.
        /// </summary>
        public ResultsCache()
        {
            _resultsByConfiguration = new ConcurrentDictionary<int, BuildResult>();
        }

        public ResultsCache(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Returns the internal cache for testing purposes.
        /// </summary>
        internal IDictionary<int, BuildResult> ResultsDictionary
        {
            get
            {
                return _resultsByConfiguration;
            }
        }

        #region IResultsCache Members

        /// <summary>
        /// Adds the specified build result to the cache
        /// </summary>
        /// <param name="result">The result to add.</param>
        public void AddResult(BuildResult result)
        {
            lock (_resultsByConfiguration)
            {
                if (_resultsByConfiguration.ContainsKey(result.ConfigurationId))
                {
                    if (Object.ReferenceEquals(_resultsByConfiguration[result.ConfigurationId], result))
                    {
                        // Merging results would be meaningless as we would be merging the object with itself.
                        return;
                    }

                    _resultsByConfiguration[result.ConfigurationId].MergeResults(result);
                }
                else
                {
                    // Note that we are not making a copy here.  This is by-design.  The TargetBuilder uses this behavior
                    // to ensure that re-entering a project will be able to see all previously built targets and avoid
                    // building them again.
                    if (!_resultsByConfiguration.TryAdd(result.ConfigurationId, result))
                    {
                        ErrorUtilities.ThrowInternalError("Failed to add result for configuration {0}", result.ConfigurationId);
                    }
                }
            }
        }

        /// <summary>
        /// Clears the results for the specified build.
        /// </summary>
        public void ClearResults()
        {
            lock (_resultsByConfiguration)
            {
                foreach (KeyValuePair<int, BuildResult> result in _resultsByConfiguration)
                {
                    result.Value.ClearCachedFiles();
                }

                _resultsByConfiguration.Clear();
            }
        }

        /// <summary>
        /// Retrieves the results for the specified build request.
        /// </summary>
        /// <param name="request">The request for which results should be retrieved.</param>
        /// <returns>The build results for the specified request.</returns>
        public BuildResult GetResultForRequest(BuildRequest request)
        {
            ErrorUtilities.VerifyThrowArgument(request.IsConfigurationResolved, "UnresolvedConfigurationInRequest");

            lock (_resultsByConfiguration)
            {
                if (_resultsByConfiguration.ContainsKey(request.ConfigurationId))
                {
                    BuildResult result = _resultsByConfiguration[request.ConfigurationId];
                    foreach (string target in request.Targets)
                    {
                        ErrorUtilities.VerifyThrow(result.HasResultsForTarget(target), "No results in cache for target " + target);
                    }

                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves the results for the specified configuration
        /// </summary>
        /// <param name="configurationId">The configuration for which results should be returned.</param>
        /// <returns>The results, if any</returns>
        public BuildResult GetResultsForConfiguration(int configurationId)
        {
            BuildResult results;
            lock (_resultsByConfiguration)
            {
                _resultsByConfiguration.TryGetValue(configurationId, out results);
            }

            return results;
        }

        /// <summary>
        /// Attempts to satisfy the request from the cache.  The request can be satisfied only if:
        /// 1. All specified targets in the request have successful results in the cache or if the sequence of target results
        ///    includes 0 or more successful targets followed by at least one failed target.
        /// 2. All initial targets in the configuration for the request have non-skipped results in the cache.
        /// 3. If there are no specified targets, then all default targets in the request must have non-skipped results
        ///    in the cache.
        /// </summary>
        /// <param name="request">The request whose results we should return</param>
        /// <param name="configInitialTargets">The initial targets for the request's configuration.</param>
        /// <param name="configDefaultTargets">The default targets for the request's configuration.</param>
        /// <param name="additionalTargetsToCheckForOverallResult">Any additional targets that need to be checked to determine overall 
        /// pass or failure, but that are not included as actual results. (E.g. AfterTargets of an entrypoint target)</param>
        /// <param name="skippedResultsDoNotCauseCacheMiss">If false, a cached skipped target will cause this method to return "NotSatisfied".  
        /// If true, then as long as there is a result in the cache (regardless of whether it was skipped or not), this method 
        /// will return "Satisfied". In most cases this should be false, but it may be set to true in a situation where there is no 
        /// chance of re-execution (which is the usual response to missing / skipped targets), and the caller just needs the data.</param>
        /// <returns>A response indicating the results, if any, and the targets needing to be built, if any.</returns>
        public ResultsCacheResponse SatisfyRequest(BuildRequest request, List<string> configInitialTargets, List<string> configDefaultTargets, List<string> additionalTargetsToCheckForOverallResult, bool skippedResultsDoNotCauseCacheMiss)
        {
            ErrorUtilities.VerifyThrowArgument(request.IsConfigurationResolved, "UnresolvedConfigurationInRequest");
            ResultsCacheResponse response = new ResultsCacheResponse(ResultsCacheResponseType.NotSatisfied);

            lock (_resultsByConfiguration)
            {
                if (_resultsByConfiguration.ContainsKey(request.ConfigurationId))
                {
                    BuildResult allResults = _resultsByConfiguration[request.ConfigurationId];

                    // Check for targets explicitly specified.
                    bool explicitTargetsSatisfied = CheckResults(allResults, request.Targets, response.ExplicitTargetsToBuild, skippedResultsDoNotCauseCacheMiss);

                    if (explicitTargetsSatisfied)
                    {
                        // All of the explicit targets, if any, have been satisfied
                        response.Type = ResultsCacheResponseType.Satisfied;

                        // Check for the initial targets.  If we don't know what the initial targets are, we assume they are not satisfied.
                        if (configInitialTargets == null || !CheckResults(allResults, configInitialTargets, null, skippedResultsDoNotCauseCacheMiss))
                        {
                            response.Type = ResultsCacheResponseType.NotSatisfied;
                        }

                        // We could still be missing implicit targets, so check those...
                        if (request.Targets.Count == 0)
                        {
                            // Check for the default target, if necessary.  If we don't know what the default targets are, we
                            // assume they are not satisfied.
                            if (configDefaultTargets == null || !CheckResults(allResults, configDefaultTargets, null, skippedResultsDoNotCauseCacheMiss))
                            {
                                response.Type = ResultsCacheResponseType.NotSatisfied;
                            }
                        }

                        // Now report those results requested, if they are satisfied.
                        if (response.Type == ResultsCacheResponseType.Satisfied)
                        {
                            List<string> targetsToAddResultsFor = new List<string>(configInitialTargets);

                            // Now report either the explicit targets or the default targets
                            if (request.Targets.Count > 0)
                            {
                                targetsToAddResultsFor.AddRange(request.Targets);
                            }
                            else
                            {
                                targetsToAddResultsFor.AddRange(configDefaultTargets);
                            }

                            response.Results = new BuildResult(request, allResults, targetsToAddResultsFor.ToArray(), additionalTargetsToCheckForOverallResult, null);
                        }
                    }
                    else
                    {
                        // Some targets were not satisfied.
                        response.Type = ResultsCacheResponseType.NotSatisfied;
                    }
                }
            }

            return response;
        }

        /// <summary>
        /// Removes the results for a particular configuration.
        /// </summary>
        /// <param name="configurationId">The configuration</param>
        public void ClearResultsForConfiguration(int configurationId)
        {
            lock (_resultsByConfiguration)
            {
                BuildResult removedResult;
                _resultsByConfiguration.TryRemove(configurationId, out removedResult);

                if (removedResult != null)
                {
                    removedResult.ClearCachedFiles();
                }
            }
        }

        public void Translate(ITranslator translator)
        {
            IDictionary<int, BuildResult> localReference = _resultsByConfiguration;

            translator.TranslateDictionary(
                ref localReference,
                (ref int i, ITranslator aTranslator) => aTranslator.Translate(ref i),
                (ref BuildResult result, ITranslator aTranslator) => aTranslator.Translate(ref result),
                capacity => new ConcurrentDictionary<int, BuildResult>(Environment.ProcessorCount, capacity));

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _resultsByConfiguration = (ConcurrentDictionary<int, BuildResult>) localReference;
            }
        }

        /// <summary>
        /// Cache as many results as we can.
        /// </summary>
        public void WriteResultsToDisk()
        {
            lock (_resultsByConfiguration)
            {
                foreach (BuildResult resultToCache in _resultsByConfiguration.Values)
                {
                    resultToCache.CacheIfPossible();
                }
            }
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Sets the build component host.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            ErrorUtilities.VerifyThrowArgumentNull(host, "host");
        }

        /// <summary>
        /// Shuts down this component
        /// </summary>
        public void ShutdownComponent()
        {
            _resultsByConfiguration.Clear();
        }

        #endregion

        /// <summary>
        /// Factory for component creation.
        /// </summary>
        internal static IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            ErrorUtilities.VerifyThrow(componentType == BuildComponentType.ResultsCache, "Cannot create components of type {0}", componentType);
            return new ResultsCache();
        }

        /// <summary>
        /// Looks for results for the specified targets.
        /// </summary>
        /// <param name="result">The result to examine</param>
        /// <param name="targets">The targets to search for</param>
        /// <param name="targetsMissingResults">An optional list to be populated with missing targets</param>
        /// <param name="skippedResultsAreOK">If true, a status of "skipped" counts as having valid results 
        /// for that target.  Otherwise, a skipped target is treated as equivalent to a missing target.</param>
        /// <returns>False if there were missing results, true otherwise.</returns>
        private static bool CheckResults(BuildResult result, List<string> targets, HashSet<string> targetsMissingResults, bool skippedResultsAreOK)
        {
            bool returnValue = true;
            foreach (string target in targets)
            {
                if (!result.HasResultsForTarget(target) || (result[target].ResultCode == TargetResultCode.Skipped && !skippedResultsAreOK))
                {
                    if (null != targetsMissingResults)
                    {
                        targetsMissingResults.Add(target);
                        returnValue = false;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    // If the result was a failure and we have not seen any skipped targets up to this point, then we conclude we do 
                    // have results for this request, and they indicate failure.
                    if (result[target].ResultCode == TargetResultCode.Failure && (targetsMissingResults == null || targetsMissingResults.Count == 0))
                    {
                        return true;
                    }
                }
            }

            return returnValue;
        }

        public IEnumerator<BuildResult> GetEnumerator()
        {
            return _resultsByConfiguration.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
