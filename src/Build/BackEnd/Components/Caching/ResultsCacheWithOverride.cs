// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Execution
{
    internal class ResultsCacheWithOverride : IResultsCache
    {
        private volatile IResultsCache? _override;
        public ResultsCache CurrentCache { get; }

        public ResultsCacheWithOverride(IResultsCache? @override = null)
        {
            _override = @override;
            CurrentCache = new ResultsCache();
        }

        internal static IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            Assumed.Equal(componentType, BuildComponentType.ResultsCache, $"Cannot create components of type {componentType}");
            return new ResultsCacheWithOverride();
        }

        internal void SetOverride(IResultsCache? @override)
        {
            _override = @override;
        }

        internal void ClearOverride()
        {
            _override = null;
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            CurrentCache.InitializeComponent(host);
        }

        public void ShutdownComponent()
        {
            _override = null;
            CurrentCache.ShutdownComponent();
        }

        public void Translate(ITranslator translator)
            => Assumed.Unreachable();

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
            IResultsCache? overrideCache = _override;
            BuildResult? overrideResult = overrideCache?.GetResultForRequest(request);
            if (overrideResult is not null)
            {
#if DEBUG
                Assumed.Null(CurrentCache.GetResultForRequest(request), "caches should not overlap");
#endif
                return overrideResult;
            }

            return CurrentCache.GetResultForRequest(request);
        }

        public BuildResult GetResultsForConfiguration(int configurationId)
        {
            IResultsCache? overrideCache = _override;
            BuildResult? overrideResult = overrideCache?.GetResultsForConfiguration(configurationId);
            if (overrideResult is not null)
            {
#if DEBUG
                Assumed.Null(CurrentCache.GetResultsForConfiguration(configurationId), "caches should not overlap");
#endif
                return overrideResult;
            }

            return CurrentCache.GetResultsForConfiguration(configurationId);
        }

        public ResultsCacheResponse SatisfyRequest(
            BuildRequest request,
            List<string> configInitialTargets,
            List<string> configDefaultTargets,
            bool skippedResultsDoNotCauseCacheMiss,
            IReadOnlyCollection<string>? allowedTopLevelTargets)
        {
            IResultsCache? overrideCache = _override;
            if (overrideCache is not null)
            {
                ResultsCacheResponse overrideRequest = overrideCache.SatisfyRequest(
                    request,
                    configInitialTargets,
                    configDefaultTargets,
                    skippedResultsDoNotCauseCacheMiss,
                    allowedTopLevelTargets);

                if (overrideRequest.Type == ResultsCacheResponseType.Satisfied)
                {
#if DEBUG
                    Assumed.Equal(CurrentCache.SatisfyRequest(
                            request,
                            configInitialTargets,
                            configDefaultTargets,
                            skippedResultsDoNotCauseCacheMiss,
                            allowedTopLevelTargets)
                            .Type, ResultsCacheResponseType.NotSatisfied, "caches should not overlap");
#endif
                    return overrideRequest;
                }
            }

            return CurrentCache.SatisfyRequest(
                request,
                configInitialTargets,
                configDefaultTargets,
                skippedResultsDoNotCauseCacheMiss,
                allowedTopLevelTargets);
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
