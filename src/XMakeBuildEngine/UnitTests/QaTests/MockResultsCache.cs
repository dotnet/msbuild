// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Mock Implementation of the results cache which does captures some counters and provides it
    /// back to the tests for validation purposes. Most of the implementation is routed to the default component
    /// by means of aggregation
    /// </summary>
    internal class QAResultsCache : IResultsCache, IBuildComponent
    {
        #region Private Data

        private IBuildComponentHost _host;
        private IResultsCache _resultCache;
        private int _clearedCount;
        private int _addCount;
        private int _getCount;

        #endregion

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        public QAResultsCache()
        {
            _resultCache = new ResultsCache();
            _addCount = 0;
            _getCount = 0;
            _clearedCount = 0;
        }

        #region IResultsCache Members

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        /// <param name="result">The result to add.</param>
        public void AddResult(BuildResult result)
        {
            _addCount++;
            _resultCache.AddResult(result);
        }

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        public void ClearResults()
        {
            _clearedCount++;
            _resultCache.ClearResults();
        }

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        /// <param name="request">The request for which results should be retrieved.</param>
        /// <returns>The build results for the specified request.</returns>
        public BuildResult GetResultForRequest(BuildRequest request)
        {
            _getCount++;
            return _resultCache.GetResultForRequest(request);
        }

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        /// <param name="configurationId">The configuration id for which results should be retrieved.</param>
        /// <returns>The build results for the specified configuration.</returns>
        public BuildResult GetResultsForConfiguration(int configurationId)
        {
            _getCount++;
            return _resultCache.GetResultsForConfiguration(configurationId);
        }

        /// <summary>
        /// Call the actual implementation.
        /// </summary>
        public ResultsCacheResponse SatisfyRequest(BuildRequest request, List<string> configInitialTargets, List<string> configDefaultTargets, List<string> additionalTargetsToCheckForOverallResult, bool skippedResultsAreOK)
        {
            return _resultCache.SatisfyRequest(request, configInitialTargets, configDefaultTargets, additionalTargetsToCheckForOverallResult, skippedResultsAreOK);
        }

        /// <summary>
        /// Clears the results for a specific configuration.
        /// </summary>
        /// <param name="configurationId">The configuration id.</param>
        public void ClearResultsForConfiguration(int configurationId)
        {
            _resultCache.ClearResultsForConfiguration(configurationId);
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void WriteResultsToDisk()
        {
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Sets the build component host.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            _host = host;
        }

        /// <summary>
        /// Shuts down this component
        /// </summary>
        public void ShutdownComponent()
        {
            _host = null;
            ((IBuildComponent)_resultCache).ShutdownComponent();
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Returns the count of the size of the cache
        /// </summary>
        public int CacheCount
        {
            get
            {
                return ((ResultsCache)_resultCache).ResultsDictionary.Count;
            }
        }

        /// <summary>
        /// Number of times the cache was checked to see if a result already existed
        /// </summary>
        public int GetCount
        {
            get
            {
                return _getCount;
            }
        }

        /// <summary>
        /// Number of times results from the cache was cleared
        /// </summary>
        public int ClearedCount
        {
            get
            {
                return _clearedCount;
            }
        }

        /// <summary>
        /// Number of results added to the cache
        /// </summary>
        public int AddCount
        {
            get
            {
                return _addCount;
            }
        }

        #endregion
    }
}
