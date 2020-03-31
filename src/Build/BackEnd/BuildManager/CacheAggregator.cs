// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    internal class CacheAggregator
    {
        private readonly Func<int> _nextConfigurationId;
        private readonly List<(IConfigCache ConfigCache, IResultsCache ResultsCache)> _inputCaches = new List<(IConfigCache ConfigCache, IResultsCache ResultsCache)>();
        private int _lastConfigurationId;
        private bool _aggregated;

        private ConfigCache _aggregatedConfigCache;
        private ResultsCache _aggregatedResultsCache;

        public CacheAggregator(Func<int> nextConfigurationId)
        {
            _nextConfigurationId = nextConfigurationId;
        }

        public void Add(IConfigCache configCache, IResultsCache resultsCache)
        {
            ErrorUtilities.VerifyThrowInternalNull(configCache, nameof(configCache));
            ErrorUtilities.VerifyThrowInternalNull(resultsCache, nameof(resultsCache));
            ErrorUtilities.VerifyThrow(!_aggregated, "Cannot add after aggregation");

            _inputCaches.Add((configCache, resultsCache));
        }

        public CacheAggregation Aggregate()
        {
            ErrorUtilities.VerifyThrow(!_aggregated, "Cannot aggregate twice");

            _aggregated = true;

            _aggregatedConfigCache = new ConfigCache();
            _aggregatedResultsCache = new ResultsCache();

            foreach (var (configCache, resultsCache) in _inputCaches)
            {
                InsertCaches(configCache, resultsCache);
            }

            return new CacheAggregation(_aggregatedConfigCache, _aggregatedResultsCache, _lastConfigurationId);
        }

        private void InsertCaches(IConfigCache configCache, IResultsCache resultsCache)
        {
            var configs = configCache.GetEnumerator().ToArray();
            var results = resultsCache.GetEnumerator().ToArray();

            ErrorUtilities.VerifyThrow(configs.Length == results.Length, "Assuming 1-to-1 mapping between configs and results. Otherwise it means the caches are either not minimal or incomplete");

            if (configs.Length == 0 && results.Length == 0)
            {
                return;
            }

            var configIdMapping = new Dictionary<int, int>();

            // seen config id -> equivalent config id already existing in the aggregated cache (null if not existing)
            var seenConfigIds = new Dictionary<int, int?>();

            foreach (var config in configs)
            {
                var existingConfig = _aggregatedConfigCache.GetMatchingConfiguration(config);

                if (existingConfig != null)
                {
                    // This config has been found in a previous cache file. Don't aggregate it.
                    // => "First config wins" conflict resolution.
                    seenConfigIds[config.ConfigurationId] = existingConfig.ConfigurationId;
                    continue;
                }

                seenConfigIds[config.ConfigurationId] = null;

                _lastConfigurationId = _nextConfigurationId();
                configIdMapping[config.ConfigurationId] = _lastConfigurationId;

                var newConfig = config.ShallowCloneWithNewId(_lastConfigurationId);
                newConfig.ResultsNodeId = Scheduler.InvalidNodeId;

                _aggregatedConfigCache.AddConfiguration(newConfig);
            }

            foreach (var result in results)
            {
                ErrorUtilities.VerifyThrow(seenConfigIds.ContainsKey(result.ConfigurationId), "Each result should have a corresponding configuration. Otherwise the caches are not consistent");

                if (seenConfigIds[result.ConfigurationId] != null)
                {
                    // The config is already present in the aggregated cache. Merge the new build results into the ones already present in the aggregated cache.
                    MergeBuildResults(result, _aggregatedResultsCache.GetResultsForConfiguration(seenConfigIds[result.ConfigurationId].Value));
                }
                else
                {
                    _aggregatedResultsCache.AddResult(
                        new BuildResult(
                            result: result,
                            submissionId: BuildEventContext.InvalidSubmissionId,
                            configurationId: configIdMapping[result.ConfigurationId],
                            requestId: BuildRequest.InvalidGlobalRequestId,
                            parentRequestId: BuildRequest.InvalidGlobalRequestId,
                            nodeRequestId: BuildRequest.InvalidNodeRequestId
                        ));
                }
            }
        }

        private void MergeBuildResults(BuildResult newResult, BuildResult existingResult)
        {
            foreach (var newTargetResult in newResult.ResultsByTarget)
            {
                // "First target result wins" conflict resolution. Seems like a reasonable heuristic, because targets in MSBuild should only run once
                // for a given config, which means that a target's result should not change if the config does not changes.
                if (!existingResult.HasResultsForTarget(newTargetResult.Key))
                {
                    existingResult.ResultsByTarget[newTargetResult.Key] = newTargetResult.Value;
                }
            }
        }
    }

    internal class CacheAggregation
    {
        public CacheAggregation(IConfigCache configCache, IResultsCache resultsCache, int lastConfigurationId)
        {
            ConfigCache = configCache;
            ResultsCache = resultsCache;
            LastConfigurationId = lastConfigurationId;
        }

        public IConfigCache ConfigCache { get; }
        public IResultsCache ResultsCache { get; }
        public int LastConfigurationId { get; }
    }
}
