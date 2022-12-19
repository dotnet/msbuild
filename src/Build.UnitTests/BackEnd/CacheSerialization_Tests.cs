// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class CacheSerialization_Tests
    {
        public static IEnumerable<object[]> CacheData {
            get
            {
                var configCache = new ConfigCache();
                var brq1 = new BuildRequestConfiguration(
                   1,
                   new BuildRequestData("path1", new Dictionary<string, string> { ["a1"] = "b1" }, Constants.defaultToolsVersion, new[] { "target1" }, null),
                   Constants.defaultToolsVersion);

                var brq2 = new BuildRequestConfiguration(
                    2,
                    new BuildRequestData("path2", new Dictionary<string, string> { ["a2"] = "b2" }, Constants.defaultToolsVersion, new[] { "target2" }, null),
                    Constants.defaultToolsVersion);
                var brq3 = new BuildRequestConfiguration(
                   3,
                   new BuildRequestData("path3", new Dictionary<string, string> { ["a3"] = "b3" }, Constants.defaultToolsVersion, new[] { "target3" }, null),
                   Constants.defaultToolsVersion);

                configCache.AddConfiguration(brq1);
                configCache.AddConfiguration(brq2);
                configCache.AddConfiguration(brq3);

                var resultsCache = new ResultsCache();
                var request1 = new BuildRequest(1, 0, 1, new string[] { "target1" }, null, BuildEventContext.Invalid, null);
                var request2 = new BuildRequest(2, 0, 2, new string[] { "target2" }, null, BuildEventContext.Invalid, null);
                var request3 = new BuildRequest(2, 0, 2, new string[] { "target2" }, null, BuildEventContext.Invalid, null);

                resultsCache.AddResult(new BuildResult(request1));
                resultsCache.AddResult(new BuildResult(request2));
                resultsCache.AddResult(new BuildResult(request3));

                return new List<object[]>
                {
                    new object[] { configCache, resultsCache },
                };
            }
        }

        [Theory]
        [MemberData(nameof(CacheData))]
        public void OnlySerializeCacheEntryWithSmallestConfigId(object configCache, object resultsCache)
        {
            string cacheFile = null;
            try
            {
                cacheFile = FileUtilities.GetTemporaryFile("MSBuildResultsCache");
                Assert.Null(CacheSerialization.SerializeCaches((ConfigCache)configCache, (ResultsCache)resultsCache, cacheFile));

                var result = CacheSerialization.DeserializeCaches(cacheFile);
                Assert.True(result.ConfigCache.HasConfiguration(1));
                Assert.False(result.ConfigCache.HasConfiguration(2));
                Assert.False(result.ConfigCache.HasConfiguration(3));
            }
            finally
            {
                File.Delete(cacheFile);
            }
        }
    }
}
