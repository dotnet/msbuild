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
    public class CacheScope_Tests
    {
        // Build result which will be added to the cache
        BuildResult resultWith0Outputs;
        BuildResult resultWith1Outputs;
        BuildResult resultWith2Outputs;
        BuildResult uncacheableResult;

        [SetUp]
        public void Initialize()
        {
            // Create some items and place them in a dictionary
            // Add some include information so that when we check the final 
            // item spec we can verify that the item was recreated properly
            BuildItem[] buildItems = new BuildItem[1];
            buildItems[0] = new BuildItem("BuildItem1", "Item1");
            Dictionary<object, object> dictionary1 = new Dictionary<object, object>();
            dictionary1.Add("Target1", buildItems);
            Hashtable resultsByTarget1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            resultsByTarget1.Add("Target1", Target.BuildState.CompletedSuccessfully);

            Dictionary<object, object> dictionary2 = new Dictionary<object, object>();
            dictionary2.Add("Target2", buildItems);
            dictionary2.Add("Target3", null);
            Hashtable resultsByTarget2 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            resultsByTarget2.Add("Target2", Target.BuildState.CompletedSuccessfully);
            resultsByTarget2.Add("Target3", Target.BuildState.CompletedSuccessfully);

            Dictionary<object, object> dictionary3 = new Dictionary<object, object>();
            dictionary3.Add("Target4", buildItems);
            Hashtable resultsByTarget3 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            resultsByTarget3.Add("Target4", Target.BuildState.Skipped);

            resultWith0Outputs = new BuildResult(new Hashtable(), new Hashtable(StringComparer.OrdinalIgnoreCase), true, 1, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
            resultWith1Outputs = new BuildResult(dictionary1, resultsByTarget1, true, 1, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
            resultWith2Outputs = new BuildResult(dictionary2, resultsByTarget2, true, 1, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
            uncacheableResult = new BuildResult(dictionary3, resultsByTarget3, true, 1, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
        }

        /// <summary>
        /// Test constructor and properties
        /// </summary>
        [Test]
        public void TestConstructor()
        {
            BuildPropertyGroup default_scope = new BuildPropertyGroup(new Project());
            Assertion.AssertNotNull(default_scope);

            CacheScope testScope = new CacheScope("Test.proj", default_scope, "2.0");
            Assert.AreEqual(testScope.ScopeProperties, default_scope, "Expected ScopeProperties to be set");

            // We should have detached the parent project from the property group, to avoid holding on to it in the cache
            Assertion.AssertEquals(null, testScope.ScopeProperties.ParentProject);
        }

        /// <summary>
        /// Test adding and removing entries
        /// </summary>
        [Test]
        public void BasicCacheOperation()
        {
            BuildPropertyGroup default_scope = new BuildPropertyGroup();
            CacheScope testScope = new CacheScope("Test.proj", new BuildPropertyGroup(), "2.0");
            // First add a single entry and verify that it is in the cache
            CacheEntry cacheEntry = new BuildResultCacheEntry("TestEntry", null, true);
            testScope.AddCacheEntry(cacheEntry);
            Assert.IsTrue(testScope.ContainsCacheEntry("TestEntry"), "Expect entry in the cache");
            CacheEntry inCacheEntry = testScope.GetCacheEntry("TestEntry");
            Assert.IsNotNull(inCacheEntry, "Cache should have an entry");
            Assert.IsTrue(inCacheEntry.IsEquivalent(cacheEntry), "Expect entry to be the same");
            // Add a second entry and then remove the first entry. Verify that the first entry
            // is not in the cache while the second entry is still there
            cacheEntry = new BuildResultCacheEntry("TestEntry2", null, true);
            testScope.AddCacheEntry(cacheEntry);
            testScope.ClearCacheEntry("TestEntry");
            Assert.IsFalse(testScope.ContainsCacheEntry("TestEntry"), "Didn't expect entry in the cache");
            Assert.IsTrue(testScope.ContainsCacheEntry("TestEntry2"), "Expected entry in the cache");
            Assert.IsNull(testScope.GetCacheEntry("TestEntry"), "Cache should  not have an entry");
            Assert.IsNotNull(testScope.GetCacheEntry("TestEntry2"), "Cache should have an entry");
        }

        /// <summary>
        /// Test adding and removing build results
        /// </summary>
        [Test]
        public void AddRemoveBuildResults()
        {
            BuildPropertyGroup default_scope = new BuildPropertyGroup();
            CacheScope testScope = new CacheScope("Test.proj", new BuildPropertyGroup(), "2.0");
            // First add a single empty result (expect no crash)
            testScope.AddCacheEntryForBuildResults(resultWith0Outputs);
            // Add a single result - expect to find target in the cache
            testScope.AddCacheEntryForBuildResults(resultWith1Outputs);
            Assert.IsTrue(testScope.ContainsCacheEntry("Target1"), "Expected entry in the cache");
            Assert.IsNotNull(testScope.GetCacheEntry("Target1"), "Cache should have an entry");
            // Add a double result expect both target in the entry
            testScope.AddCacheEntryForBuildResults(resultWith2Outputs);
            Assert.IsTrue(testScope.ContainsCacheEntry("Target2"), "Expected entry in the cache");
            Assert.IsNotNull(testScope.GetCacheEntry("Target2"), "Cache should have an entry");
            Assert.IsTrue(testScope.ContainsCacheEntry("Target3"), "Expected entry in the cache");
            Assert.IsNotNull(testScope.GetCacheEntry("Target3"), "Cache should have an entry");
            // Double add a result ( expect no crash since it is identical )
            testScope.AddCacheEntryForBuildResults(resultWith1Outputs);
            // Add an uncacheable result and verify that it is not in the cache
            testScope.AddCacheEntryForBuildResults(uncacheableResult);
            Assert.IsFalse(testScope.ContainsCacheEntry("Target4"), "Didn't expect entry in the cache");
            Assert.IsNull(testScope.GetCacheEntry("Target4"), "Cache should  not have an entry");
        }
    }
}
