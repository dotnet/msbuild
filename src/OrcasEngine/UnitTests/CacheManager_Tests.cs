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
    public class CacheManager_Tests
    {
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

            resultWithOutputs = new BuildResult(dictionary, resultByTargetSuccess, true, 1, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
            failedResult = new BuildResult(dictionary, resultByTargetFailure, false, 1, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
            uncacheableResult = new BuildResult(dictionary, resultByTargetSkipped, true, 1, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
        }

        /// <summary>
        /// Test basic operation add/remove
        /// </summary>
        [Test]
        public void CheckBasicOperation()
        {
            CacheManager cacheManager = new CacheManager("3.5");
            CacheScope cacheScope = cacheManager.GetCacheScope("Test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.Items);
            Assert.IsNotNull(cacheScope, "Cache should not have an entry");
            CacheScope cacheScope1 = cacheManager.GetCacheScope("Test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.Items);
            Assert.AreEqual(cacheScope, cacheScope1, "Expected to get the same scope");
            cacheScope1 = cacheManager.GetCacheScope("Test1.proj", new BuildPropertyGroup(), "3.5", CacheContentType.Items);
            Assert.IsNotNull(cacheScope1, "Cache should not have an entry");
            Assert.AreNotEqual(cacheScope, cacheScope1, "Expected to get different scopes");
            // Add an entry and verify that it ends up in the right scope
            CacheEntry cacheEntry = new BuildResultCacheEntry("TestEntry", null, true);
            CacheEntry cacheEntry1 = new BuildResultCacheEntry("TestEntry1", null, true);
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry }, "Test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.Items);
            Assert.IsNotNull(cacheScope.GetCacheEntry("TestEntry"), "Cache should have an entry");
            Assert.IsNotNull(cacheManager.GetCacheEntries(new string[] { "TestEntry" }, "Test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.Items)[0],
                             "Cache should have an entry");
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry1 }, "Test1.proj", new BuildPropertyGroup(), "3.5", CacheContentType.Items);
            Assert.IsNotNull(cacheScope1.GetCacheEntry("TestEntry1"), "Cache should have an entry");
            Assert.IsNotNull(cacheManager.GetCacheEntries(new string[] { "TestEntry1" }, "Test1.proj", new BuildPropertyGroup(), "3.5", CacheContentType.Items)[0],
                 "Cache should have an entry");
            // Try clearing the whole cache
            cacheManager.ClearCache();
            Assert.AreNotEqual(cacheScope, cacheManager.GetCacheScope("Test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.Items),
                               "Expected to get different scopes");
        }


        /// <summary>
        /// Test basic operation add/remove
        /// </summary>
        [Test]
        public void TestRequestCaching()
        {
            CacheManager cacheManager = new CacheManager("3.5");

            ArrayList actuallyBuiltTargets;

            // Test the case where we pass in null targets
            Dictionary<string, string> dictionary = new Dictionary<string,string>();
            BuildRequest emptyRequest = new BuildRequest(1, "test.proj", null, dictionary, null, 1, true, false);

            Assert.IsNull(cacheManager.GetCachedBuildResult(emptyRequest, out actuallyBuiltTargets), "Expect a null return value if T=null");

            // Test the case where we pass in length 0 targets
            BuildRequest length0Request = new BuildRequest(1, "test.proj", new string[0], dictionary, null, 1, true, false);
            Assert.IsNull(cacheManager.GetCachedBuildResult(length0Request, out actuallyBuiltTargets), "Expect a null return value if T.Length=0");

            // Test the case when the scope doesn't exist
            string[] targets = new string[1]; targets[0] = "Target1";
            BuildRequest length1Request = new BuildRequest(1, "test.proj", targets, new BuildPropertyGroup(), null, 1, true, false);
            Assert.IsNull(cacheManager.GetCachedBuildResult(length1Request, out actuallyBuiltTargets), "Expect a null return value if no scope");
            
            // Test the case when the scope exists but is empty
            CacheScope cacheScope = cacheManager.GetCacheScope("test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.BuildResults);
            Assert.IsNull(cacheManager.GetCachedBuildResult(length1Request, out actuallyBuiltTargets), "Expect a null return value if scope is empty");
            
            // Test the case when the scope exists but contains wrong data
            CacheEntry cacheEntry = new BuildResultCacheEntry("Target2", null, true);
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry }, "test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.BuildResults);
            Assert.IsNull(cacheManager.GetCachedBuildResult(length1Request, out actuallyBuiltTargets), "Expect a null return value if scope contains wrong data");
            
            // Test the case when everything is correct
            cacheScope.AddCacheEntry(new PropertyCacheEntry(Constants.defaultTargetCacheName, string.Empty));
            cacheScope.AddCacheEntry(new PropertyCacheEntry(Constants.initialTargetCacheName, string.Empty));
            cacheScope.AddCacheEntry(new PropertyCacheEntry(Constants.projectIdCacheName, "1"));
            
            cacheEntry = new BuildResultCacheEntry("Target1", null, true);
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry }, "test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.BuildResults);
            BuildResult buildResult = cacheManager.GetCachedBuildResult(length1Request, out actuallyBuiltTargets);
            Assert.IsNotNull(buildResult, "Expect a cached value if scope contains data");
            Assert.AreEqual(1, actuallyBuiltTargets.Count);
            Assert.AreEqual("Target1", actuallyBuiltTargets[0]);
            Assert.AreEqual(1, buildResult.ResultByTarget.Count);
            Assert.AreEqual(Target.BuildState.CompletedSuccessfully, buildResult.ResultByTarget["Target1"]);

            // Test the case when the scope contains partially correct data
            targets = new string[2]; targets[0] = "Target2"; targets[1] = "Target3";
            BuildRequest length2Request = new BuildRequest(1, "test.proj", targets, new BuildPropertyGroup(), null, 1, true, false);
            Assert.IsNull(cacheManager.GetCachedBuildResult(length2Request, out actuallyBuiltTargets), "Expect a null return value if partial data in the scope");
            
            // Test the correctness case for multiple targets
            cacheEntry = new BuildResultCacheEntry("Target3", null, true);
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry }, "test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.BuildResults);
            buildResult = cacheManager.GetCachedBuildResult(length2Request, out actuallyBuiltTargets);
            Assert.IsNotNull(buildResult, "Expect a cached value if scope contains data");

            Assert.AreEqual(2, actuallyBuiltTargets.Count);
            Assert.AreEqual("Target2", actuallyBuiltTargets[0]);
            Assert.AreEqual("Target3", actuallyBuiltTargets[1]);
            Assert.AreEqual(2, buildResult.ResultByTarget.Count);
            Assert.AreEqual(Target.BuildState.CompletedSuccessfully, buildResult.ResultByTarget["Target2"]);
            Assert.AreEqual(Target.BuildState.CompletedSuccessfully, buildResult.ResultByTarget["Target3"]);
            Assert.AreEqual(1, buildResult.ProjectId);
        }

        /// <summary>
        /// Test request caching with default/initial targets
        /// </summary>
        [Test]
        public void TestRequestCachingDefaultInitialTargets()
        {
            CacheManager cacheManager = new CacheManager("3.5");

            ArrayList actuallyBuiltTargets;
            CacheScope cacheScope = cacheManager.GetCacheScope("test.proj", new BuildPropertyGroup(), null, CacheContentType.BuildResults);
            cacheScope.AddCacheEntry(new PropertyCacheEntry(Constants.defaultTargetCacheName, "Target1;Target2"));
            cacheScope.AddCacheEntry(new PropertyCacheEntry(Constants.initialTargetCacheName, "Initial1"));
            cacheScope.AddCacheEntry(new PropertyCacheEntry(Constants.projectIdCacheName, "5"));
            
            CacheEntry cacheEntry = new BuildResultCacheEntry("Initial1", null, true);
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry }, "test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.BuildResults);
            cacheEntry = new BuildResultCacheEntry("Target1", null, true);
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry }, "test.proj", new BuildPropertyGroup(), null, CacheContentType.BuildResults);
            cacheEntry = new BuildResultCacheEntry("Target2", null, false);
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry }, "test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.BuildResults);
            cacheEntry = new BuildResultCacheEntry("Target3", null, true);
            cacheManager.SetCacheEntries(new CacheEntry[] { cacheEntry }, "test.proj", new BuildPropertyGroup(), null, CacheContentType.BuildResults);

            // Default target
            BuildRequest defaultRequest = new BuildRequest(1, "test.proj", null, new BuildPropertyGroup(), null, 1, true, false);
            BuildResult buildResult = cacheManager.GetCachedBuildResult(defaultRequest, out actuallyBuiltTargets);
            Assert.IsNotNull(buildResult, "Expect a cached value if scope contains data");

            Assert.AreEqual(3, actuallyBuiltTargets.Count);
            Assert.AreEqual("Initial1", actuallyBuiltTargets[0]);
            Assert.AreEqual("Target1", actuallyBuiltTargets[1]);
            Assert.AreEqual("Target2", actuallyBuiltTargets[2]);
            Assert.AreEqual(3, buildResult.ResultByTarget.Count);
            Assert.AreEqual(Target.BuildState.CompletedSuccessfully, buildResult.ResultByTarget["Initial1"]);
            Assert.AreEqual(Target.BuildState.CompletedSuccessfully, buildResult.ResultByTarget["Target1"]);
            Assert.AreEqual(Target.BuildState.CompletedUnsuccessfully, buildResult.ResultByTarget["Target2"]);
            Assert.AreEqual(false, buildResult.EvaluationResult);
            Assert.AreEqual(5, buildResult.ProjectId);

            // Specific target
            BuildRequest specificRequest = new BuildRequest(1, "test.proj", new string[] { "Target3" }, new BuildPropertyGroup(), null, 1, true, false);
            buildResult = cacheManager.GetCachedBuildResult(specificRequest, out actuallyBuiltTargets);
            Assert.IsNotNull(buildResult, "Expect a cached value if scope contains data");

            Assert.AreEqual(2, actuallyBuiltTargets.Count);
            Assert.AreEqual("Initial1", actuallyBuiltTargets[0]);
            Assert.AreEqual("Target3", actuallyBuiltTargets[1]);
            Assert.AreEqual(2, buildResult.ResultByTarget.Count);
            Assert.AreEqual(Target.BuildState.CompletedSuccessfully, buildResult.ResultByTarget["Initial1"]);
            Assert.AreEqual(Target.BuildState.CompletedSuccessfully, buildResult.ResultByTarget["Target3"]);
            Assert.AreEqual(true, buildResult.EvaluationResult);
            Assert.AreEqual(5, buildResult.ProjectId);
        }

        /// <summary>
        /// Test Clearing Cache Scope
        /// </summary>
        [Test]
        public void TestClearCacheScope()
        {
            CacheManager cacheManager = new CacheManager("3.5");
            CacheScope cacheScope = cacheManager.GetCacheScope("test.proj", new BuildPropertyGroup(), null, CacheContentType.BuildResults);
            cacheScope.AddCacheEntry(new PropertyCacheEntry(Constants.defaultTargetCacheName, "Target1;Target2"));
            cacheScope.AddCacheEntry(new PropertyCacheEntry(Constants.initialTargetCacheName, "Initial1"));
            cacheManager.ClearCacheScope("test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.BuildResults);
            Assert.AreNotEqual(cacheScope, cacheManager.GetCacheScope("test.proj", new BuildPropertyGroup(), "3.5", CacheContentType.BuildResults), "Expected to get different scopes");
        }
    }
}
