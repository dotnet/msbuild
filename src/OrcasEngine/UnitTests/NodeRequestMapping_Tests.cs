// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class NodeRequestMapping_Tests
    {
        // Project to which the results will be cached to
        CacheScope cacheScope;
        // Build result which will be added to the cache
        BuildResult resultWithOutputs;
        // Build result marked as should not cache
        BuildResult uncacheableResult;
        // Build result that was failed
        BuildResult failedResult;

        [SetUp]
        public void Initialize()
        {
            // Create some items and place them in a dictionary
            // Add some include information so that when we check the final 
            // item spec we can verify that the item was recreated properly
            BuildItem buildItem1 = new BuildItem("BuildItem1", "Item1");
            buildItem1.Include = "TestInclude1";
            BuildItem[] buildItems = new BuildItem[1];
            buildItems[0] = buildItem1;
            Dictionary<object, object> dictionary = new Dictionary<object, object>();
            dictionary.Add("TaskItems", buildItems);

            Hashtable resultByTargetSuccess = new Hashtable(StringComparer.OrdinalIgnoreCase);
            resultByTargetSuccess.Add("TaskItems", Target.BuildState.CompletedSuccessfully);
            Hashtable resultByTargetFailure = new Hashtable(StringComparer.OrdinalIgnoreCase);
            resultByTargetFailure.Add("TaskItems", Target.BuildState.CompletedUnsuccessfully);
            Hashtable resultByTargetSkipped = new Hashtable(StringComparer.OrdinalIgnoreCase);
            resultByTargetSkipped.Add("TaskItems", Target.BuildState.Skipped);

            resultWithOutputs = new BuildResult(dictionary, resultByTargetSuccess, true, 1, 1, 3, true, string.Empty, string.Empty, 0, 0, 0);
            failedResult = new BuildResult(dictionary, resultByTargetFailure, false, 1, 1, 3, true, string.Empty, string.Empty, 0, 0, 0);
            uncacheableResult = new BuildResult(dictionary, resultByTargetSkipped, true, 1, 1, 3, true, string.Empty, string.Empty, 0, 0, 0);

            cacheScope = new CacheScope("temp.proj", new BuildPropertyGroup(), "3.5");
        }

        /// <summary>
        /// Test AddResultToCache using a "normal" constructor call
        /// </summary>
        [Test]
        public void AddResultToCache()
        {
            Assert.IsNull(cacheScope.GetCacheEntry("TaskItems"), "Cache should not have an entry");
            NodeRequestMapping requestMapping = new NodeRequestMapping(1, 1, cacheScope);
            Assert.AreEqual(1,requestMapping.HandleId,"Expected NodeProxyId to be 1");
            Assert.AreEqual(1,requestMapping.RequestId,"Expected RequestId to be 1");
            requestMapping.AddResultToCache(resultWithOutputs);
            Assert.IsTrue(resultWithOutputs.EvaluationResult == ((BuildResultCacheEntry)cacheScope.GetCacheEntry("TaskItems")).BuildResult, 
                "Expected EvaluationResult to be the same after it was retrieved from the cache");
            Assert.IsTrue(((BuildItem[])resultWithOutputs.OutputsByTarget["TaskItems"])[0].Include == ((BuildResultCacheEntry)cacheScope.GetCacheEntry("TaskItems")).BuildItems[0].Include,
                "Expected EvaluationResult to be the same after it was retrieved from the cache");
            // Remove the entry from the cache
            cacheScope.ClearCacheEntry("TaskItems");
        }

        /// <summary>
        /// If no the result is marked as uncacheable it should not be cached
        /// </summary>
        [Test]
        public void AddResultToCacheUncacheableResult()
        {
            Assert.IsNull(cacheScope.GetCacheEntry("TaskItems"), "Cache should not have an entry");
            NodeRequestMapping requestMapping = new NodeRequestMapping(1, 1, cacheScope);
            Assert.AreEqual(1, requestMapping.HandleId, "Expected NodeProxyId to be 1");
            Assert.AreEqual(1, requestMapping.RequestId, "Expected RequestId to be 1");
            requestMapping.AddResultToCache(uncacheableResult);
            Assert.IsNull(cacheScope.GetCacheEntry("TaskItems"), 
                "Expected null to be retrieved from the cache as the targetNamesList should not have been added");
        }

        /// <summary>
        /// If no the result is failed it should not be cached
        /// </summary>
        [Test]
        public void AddResultToCacheFailedResult()
        {
            Assert.IsNull(cacheScope.GetCacheEntry("TaskItems"), "Cache should not have an entry");
            NodeRequestMapping requestMapping = new NodeRequestMapping(1, 1, cacheScope);
            Assert.AreEqual(1, requestMapping.HandleId, "Expected NodeProxyId to be 1");
            Assert.AreEqual(1, requestMapping.RequestId, "Expected RequestId to be 1");
            requestMapping.AddResultToCache(failedResult);
            Assert.IsTrue(failedResult.EvaluationResult == ((BuildResultCacheEntry)cacheScope.GetCacheEntry("TaskItems")).BuildResult,
                "Expected EvaluationResult to be the same after it was retrieved from the cache");
            Assert.IsNull(((BuildResultCacheEntry)cacheScope.GetCacheEntry("TaskItems")).BuildItems,
                "Task items should not be cached for failed build results");
            // Remove the entry from the cache
            cacheScope.ClearCacheEntry("TaskItems");
        }

        /// <summary>
        /// Internal error exception should be thrown if null cache scope is passed in. This test is
        /// here to describe the behavior but it is not run because it causes a pop up.
        /// </summary>
        /* [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void AddResultToPassInNullCacheScope()
        {
            NodeRequestMapping requestMapping = new NodeRequestMapping(1, 1, null );
        } */

        /// <summary>
        /// Make sure that the correct InternalErrorException exception is thrown if a null build result is 
        /// attempted to be cached. This test is
        /// here to describe the behavior but it is not run because it causes a pop up.
        /// </summary>
        /*[Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void AddResultToCacheNullBuildResult()
        {
            NodeRequestMapping requestMapping = new NodeRequestMapping(1, 1, cacheScope);
            Assert.AreEqual(1, requestMapping.NodeProxyId, "Expected NodeProxyId to be 1");
            Assert.AreEqual(1, requestMapping.RequestId, "Expected RequestId to be 1");
            requestMapping.AddResultToCache(null);
        }*/

    }
}
