// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    internal class ResultsCacheWithOverride : IResultsCache
    {
        private readonly IResultsCache _override;
        public ResultsCache CurrentCache { get; }


        public ResultsCacheWithOverride(IResultsCache @override)
        {
            _override = @override;
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
#if DEBUG
                ErrorUtilities.VerifyThrow(CurrentCache.GetResultForRequest(request) == null, "caches should not overlap");
#endif
                return overrideResult;
            }

            return CurrentCache.GetResultForRequest(request);
        }

        public BuildResult GetResultsForConfiguration(int configurationId)
        {
            var overrideResult = _override.GetResultsForConfiguration(configurationId);
            if (overrideResult != null)
            {
#if DEBUG
                ErrorUtilities.VerifyThrow(CurrentCache.GetResultsForConfiguration(configurationId) == null, "caches should not overlap");
#endif
                return overrideResult;
            }

            return CurrentCache.GetResultsForConfiguration(configurationId);
        }

        public ResultsCacheResponse SatisfyRequest(
            BuildRequest request,
            List<string> configInitialTargets,
            List<string> configDefaultTargets,
            List<string> additionalTargetsToCheckForOverallResult,
            bool skippedResultsDoNotCauseCacheMiss)
        {
            var overrideRequest = _override.SatisfyRequest(
                request,
                configInitialTargets,
                configDefaultTargets,
                additionalTargetsToCheckForOverallResult,
                skippedResultsDoNotCauseCacheMiss);

            if (overrideRequest.Type == ResultsCacheResponseType.Satisfied)
            {
#if DEBUG
                ErrorUtilities.VerifyThrow(
                    CurrentCache.SatisfyRequest(
                        request,
                        configInitialTargets,
                        configDefaultTargets,
                        additionalTargetsToCheckForOverallResult,
                        skippedResultsDoNotCauseCacheMiss)
                        .Type == ResultsCacheResponseType.NotSatisfied,
                    "caches should not overlap");
#endif
                return overrideRequest;
            }

            return CurrentCache.SatisfyRequest(
                request,
                configInitialTargets,
                configDefaultTargets,
                additionalTargetsToCheckForOverallResult,
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
            return CurrentCache.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
