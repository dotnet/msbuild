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

            var seenConfigIds = new HashSet<int>();
            var configIdMapping = new Dictionary<int, int>();

            foreach (var config in configs)
            {
                seenConfigIds.Add(config.ConfigurationId);

                ErrorUtilities.VerifyThrow(_aggregatedConfigCache.GetMatchingConfiguration(config) == null, "Input caches should not contain entries for the same configuration");

                _lastConfigurationId = _nextConfigurationId();
                configIdMapping[config.ConfigurationId] = _lastConfigurationId;

                var newConfig = config.ShallowCloneWithNewId(_lastConfigurationId);
                newConfig.ResultsNodeId = Scheduler.InvalidNodeId;

                _aggregatedConfigCache.AddConfiguration(newConfig);
            }

            foreach (var result in results)
            {
                ErrorUtilities.VerifyThrow(seenConfigIds.Contains(result.ConfigurationId), "Each result should have a corresponding configuration. Otherwise the caches are not consistent");

                _aggregatedResultsCache.AddResult(
                    new BuildResult(
                        result,
                        BuildEventContext.InvalidSubmissionId,
                        configIdMapping[result.ConfigurationId],
                        BuildRequest.InvalidGlobalRequestId,
                        BuildRequest.InvalidGlobalRequestId,
                        BuildRequest.InvalidNodeRequestId
                        ));
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
