// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    // This class composes two caches, an override cache and a current cache.
    // Reads are served from both caches (override first)
    // Writes should only happen in the current cache.
    internal class ResultsCacheWithOverride : IResultsCache
    {
        private readonly IResultsCache _override;
        private readonly bool _isolateProjects;
        private readonly ConfigCacheWithOverride _configCacheWithOverride;
        public ResultsCache CurrentCache { get; }


        public ResultsCacheWithOverride(IResultsCache @override, bool isolateProjects,
            ConfigCacheWithOverride configCacheWithOverride)
        {
            _override = @override;
            _isolateProjects = isolateProjects;
            _configCacheWithOverride = configCacheWithOverride;

            CurrentCache = new ResultsCache();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            CurrentCache.InitializeComponent(host);
        }

        public void ShutdownComponent()
        {
            CurrentCache.ShutdownComponent();
        }

        public void Translate(ITranslator translator)
        {
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        public void AddResult(BuildResult result)
        {
            CurrentCache.AddResult(result);

            _configCacheWithOverride.BuildResultAddedForConfiguration(result.ConfigurationId);
        }

        public void ClearResults()
        {
            CurrentCache.ClearResults();
        }

        public BuildResult GetResultForRequest(BuildRequest request)
        {
            var overrideResult = _override.GetResultForRequest(request);

            if (overrideResult != null)
            {
                AssertCurrentCacheDoesNotContainResult(overrideResult);
                return overrideResult;
            }

            return CurrentCache.GetResultForRequest(request);
        }

        public BuildResult GetResultsForConfiguration(int configurationId)
        {
            var overrideResult = _override.GetResultsForConfiguration(configurationId);
            if (overrideResult != null)
            {
                AssertCurrentCacheDoesNotContainResult(overrideResult);
                return overrideResult;
            }

            return CurrentCache.GetResultsForConfiguration(configurationId);
        }

        public ResultsCacheResponse SatisfyRequest(
            BuildRequest request,
            List<string> configInitialTargets,
            List<string> configDefaultTargets,
            bool skippedResultsDoNotCauseCacheMiss)
        {
            var overrideRequest = _override.SatisfyRequest(
                request,
                configInitialTargets,
                configDefaultTargets,
                skippedResultsDoNotCauseCacheMiss);

            if (overrideRequest.Type == ResultsCacheResponseType.Satisfied)
            {
                AssertCurrentCacheDoesNotContainResult(_override.GetResultsForConfiguration(request.ConfigurationId));

                return overrideRequest;
            }

            return CurrentCache.SatisfyRequest(
                request,
                configInitialTargets,
                configDefaultTargets,
                skippedResultsDoNotCauseCacheMiss);
        }

        public void ClearResultsForConfiguration(int configurationId)
        {
            CurrentCache.ClearResultsForConfiguration(configurationId);
        }

        public void WriteResultsToDisk()
        {
            CurrentCache.WriteResultsToDisk();
        }

        public IEnumerator<BuildResult> GetEnumerator()
        {
            // Enumerators do not compose both caches to limit the influence of the override cache (reduce the number of possible states out there).
            // So far all runtime examples do not need the two composed.
            return CurrentCache.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void AssertCurrentCacheDoesNotContainResult(BuildResult overrideResult)
        {
            // There could be an exempt project being built for which there is already an entry in the override cache (if the exempt project is also present
            // in an input cache, for example if a project both exempts a reference, and also has a ProjectReference on it).
            // In this situation, the exempt project may be built with additional targets for which there are no results in the override cache.
            // This will cause the newly built targets to be saved both in the override cache, and also in the current cache.
            // For this particular case, skip the check that a BuildResult for a particular configuration id should be in only one of the caches, not both.
            var skipCheck = _isolateProjects && _configCacheWithOverride[overrideResult.ConfigurationId].SkippedFromStaticGraphIsolationConstraints;

            if (!skipCheck)
            {
                ErrorUtilities.VerifyThrow(CurrentCache.GetResultsForConfiguration(overrideResult.ConfigurationId) == null, "caches should not overlap");
            }
        }
    }
}
