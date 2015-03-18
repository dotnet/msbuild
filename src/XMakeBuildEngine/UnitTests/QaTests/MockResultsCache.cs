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

        private IBuildComponentHost host;
        private IResultsCache resultCache;
        private int clearedCount;
        private int addCount;
        private int getCount;

        #endregion

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        public QAResultsCache()
        {
            this.resultCache = new ResultsCache();
            this.addCount = 0;
            this.getCount = 0;
            this.clearedCount = 0;
        }

        #region IResultsCache Members

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        /// <param name="result">The result to add.</param>
        public void AddResult(BuildResult result)
        {
            addCount++;
            this.resultCache.AddResult(result);
        }

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        public void ClearResults()
        {
            clearedCount++;
            this.resultCache.ClearResults();
        }

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        /// <param name="request">The request for which results should be retrieved.</param>
        /// <returns>The build results for the specified request.</returns>
        public BuildResult GetResultForRequest(BuildRequest request)
        {
            getCount++;
            return this.resultCache.GetResultForRequest(request);
        }

        /// <summary>
        /// Call the actual implementation
        /// </summary>
        /// <param name="configurationId">The configuration id for which results should be retrieved.</param>
        /// <returns>The build results for the specified configuration.</returns>
        public BuildResult GetResultsForConfiguration(int configurationId)
        {
            getCount++;
            return this.resultCache.GetResultsForConfiguration(configurationId);
        }

        /// <summary>
        /// Call the actual implementation.
        /// </summary>
        public ResultsCacheResponse SatisfyRequest(BuildRequest request, List<string> configInitialTargets, List<string> configDefaultTargets, List<string> additionalTargetsToCheckForOverallResult, bool skippedResultsAreOK)
        {
            return this.resultCache.SatisfyRequest(request, configInitialTargets, configDefaultTargets, additionalTargetsToCheckForOverallResult, skippedResultsAreOK);
        }

        /// <summary>
        /// Clears the results for a specific configuration.
        /// </summary>
        /// <param name="configurationId">The configuration id.</param>
        public void ClearResultsForConfiguration(int configurationId)
        {
            this.resultCache.ClearResultsForConfiguration(configurationId);
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
            this.host = host;
        }

        /// <summary>
        /// Shuts down this component
        /// </summary>
        public void ShutdownComponent()
        {
            host = null;
            ((IBuildComponent)this.resultCache).ShutdownComponent();
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
                return ((ResultsCache)this.resultCache).ResultsDictionary.Count;
            }
        }

        /// <summary>
        /// Number of times the cache was checked to see if a result already existed
        /// </summary>
        public int GetCount
        {
            get
            {
                return this.getCount;
            }
        }

        /// <summary>
        /// Number of times results from the cache was cleared
        /// </summary>
        public int ClearedCount
        {
            get
            {
                return this.clearedCount;
            }
        }

        /// <summary>
        /// Number of results added to the cache
        /// </summary>
        public int AddCount
        {
            get
            {
                return this.addCount;
            }
        }

        #endregion
    }
}
