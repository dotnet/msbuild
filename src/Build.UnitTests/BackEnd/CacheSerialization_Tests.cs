// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        public static IEnumerable<object[]> CacheData
        {
            get
            {
                var configCache = new ConfigCache();
                var brq1 = new BuildRequestConfiguration(
                    1,
                    new BuildRequestData("path1", new Dictionary<string, string> { ["a1"] = "b1" }, Constants.defaultToolsVersion, new[] { "target1", "target2" }, null),
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
                var request1 = new BuildRequest(1, 0, 1, new string[] { "target1", "target2", "target3" }, null, BuildEventContext.Invalid, null);
                var request2 = new BuildRequest(2, 0, 2, new string[] { "target2" }, null, BuildEventContext.Invalid, null);
                var request3 = new BuildRequest(3, 0, 3, new string[] { "target3" }, null, BuildEventContext.Invalid, null);

                var buildResult1 = new BuildResult(request1);
                var buildResult2 = new BuildResult(request2);
                var buildResult3 = new BuildResult(request3);

                buildResult1.AddResultsForTarget(
                    "target1",
                    new TargetResult(
                        Array.Empty<ProjectItemInstance.TaskItem>(),
                        new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null)));
                buildResult1.AddResultsForTarget(
                    "target2",
                    new TargetResult(
                        Array.Empty<ProjectItemInstance.TaskItem>(),
                        new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null)));
                buildResult1.AddResultsForTarget(
                    "target3",
                    new TargetResult(
                        Array.Empty<ProjectItemInstance.TaskItem>(),
                        new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null)));

                buildResult2.AddResultsForTarget(
                    "target2",
                    new TargetResult(
                        Array.Empty<ProjectItemInstance.TaskItem>(),
                        new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null)));

                buildResult3.AddResultsForTarget(
                    "target3",
                    new TargetResult(
                        Array.Empty<ProjectItemInstance.TaskItem>(),
                        new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null)));

                resultsCache.AddResult(buildResult1);
                resultsCache.AddResult(buildResult2);
                resultsCache.AddResult(buildResult3);

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
                Assert.Null(CacheSerialization.SerializeCaches(
                    (ConfigCache)configCache,
                    (ResultsCache)resultsCache,
                    cacheFile,
                    ProjectIsolationMode.True));

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

        [Theory]
        [MemberData(nameof(CacheData))]
        public void OnlySerializeResultsForSpecifiedTargets(object configCache, object resultsCache)
        {
            // Setup:
            // 1. Create a config with id 1 whose project is built with top-level targets target1
            // and target2.
            // 2. Send a build request and collect the BuildResults for targets target1, target2,
            // and target3.
            // 3. Ensure the BuildResult for target3 is excluded from output cache serialization
            // since it's not a top-level target.
            string cacheFile = null;
            try
            {
                cacheFile = FileUtilities.GetTemporaryFile("MSBuildResultsCache");
                Assert.Null(CacheSerialization.SerializeCaches(
                    (ConfigCache)configCache,
                    (ResultsCache)resultsCache,
                    cacheFile,
                    ProjectIsolationMode.MessageUponIsolationViolation));

                var result = CacheSerialization.DeserializeCaches(cacheFile);
                Assert.True(result.ConfigCache.HasConfiguration(1));
                BuildResult buildResult = result.ResultsCache.GetResultsForConfiguration(1);
                Assert.True(buildResult.HasResultsForTarget("target1"));
                Assert.True(buildResult.HasResultsForTarget("target2"));
                Assert.False(buildResult.HasResultsForTarget("target3"));
            }
            finally
            {
                File.Delete(cacheFile);
            }
        }
    }
}
