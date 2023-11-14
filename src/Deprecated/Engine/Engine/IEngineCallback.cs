// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;

namespace Microsoft.Build.BuildEngine
{
    internal interface IEngineCallback
    {
        /// <summary>
        /// This method is called by a child engine or node provider to request the parent engine
        /// to build a certain part of the tree which is needed to complete an earlier request
        /// received from the parent engine. The parent engine is expected to
        /// pass back buildResult once the build is completed
        /// </summary>
        void PostBuildRequestsToHost(BuildRequest[] buildRequests);

        /// <summary>
        /// This method is called to send results to the parent engine in response to an earlier
        /// build request.
        /// </summary>
        /// <param name="buildResult"></param>
        void PostBuildResultToHost(BuildResult buildResult);

        /// <summary>
        /// This method is used to send logging events to the parent engine
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="nodeLoggingEventArray"></param>
        void PostLoggingMessagesToHost(int nodeId, NodeLoggingEvent[] nodeLoggingEventArray);

        /// <summary>
        /// Posts the given set of cache entries to the parent engine.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="entries"></param>
        /// <param name="scopeName"></param>
        /// <param name="scopeProperties"></param>
        /// <param name="scopeToolsVersion"></param>
        Exception PostCacheEntriesToHost(int nodeId, CacheEntry[] entries, string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion, CacheContentType cacheContentType);

        /// <summary>
        /// Retrieves the requested set of cache entries from the engine.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="names"></param>
        /// <param name="scopeName"></param>
        /// <param name="scopeProperties"></param>
        /// <param name="scopeToolsVersion"></param>
        /// <returns></returns>
        CacheEntry[] GetCachedEntriesFromHost(int nodeId, string[] names, string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion, CacheContentType cacheContentType);

        /// <summary>
        /// This method is called to post current status to the parent
        /// </summary>
        /// <param name="nodeId"> The identifer for the node </param>
        /// <param name="nodeStatus">The filled out status structure</param>
        /// <param name="blockUntilSent">If true the call will not return until the data has been
        ///                              written out.</param>
        void PostStatus(int nodeId, NodeStatus nodeStatus, bool blockUntilSent);
    }
}
