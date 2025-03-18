// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Unittest;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class ResultsCache_Tests
    {
        [Fact]
        public void TestConstructor()
        {
            ResultsCache cache = new ResultsCache();
        }

        [Fact]
        public void TestAddAndRetrieveResults()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget" }, null, BuildEventContext.Invalid, null); BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", BuildResultUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            BuildResult retrievedResult = cache.GetResultForRequest(request);

            Assert.True(AreResultsIdentical(result, retrievedResult));
        }

        [Fact]
        public void TestAddAndRetrieveResultsByConfiguration()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget" }, null, BuildEventContext.Invalid, null);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", BuildResultUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "otherTarget" }, null, BuildEventContext.Invalid, null);
            result = new BuildResult(request);
            result.AddResultsForTarget("otherTarget", BuildResultUtilities.GetEmptySucceedingTargetResult());
            cache.AddResult(result);

            BuildResult retrievedResult = cache.GetResultsForConfiguration(1);

            Assert.True(retrievedResult.HasResultsForTarget("testTarget"));
            Assert.True(retrievedResult.HasResultsForTarget("otherTarget"));
        }

        [Fact]
        public void CacheCanBeEnumerated()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(submissionId: 1, nodeRequestId: 0, configurationId: 1, new string[1] { "testTarget" }, null, BuildEventContext.Invalid, null);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("result1target1", BuildResultUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "otherTarget" }, null, BuildEventContext.Invalid, null);
            result = new BuildResult(request);
            result.AddResultsForTarget("result1target2", BuildResultUtilities.GetEmptySucceedingTargetResult());
            cache.AddResult(result);

            BuildResult result2 = new BuildResult(new BuildRequest(submissionId: 1, nodeRequestId: 0, configurationId: 2, new string[1] { "testTarget" }, null, BuildEventContext.Invalid, null));
            result2.AddResultsForTarget("result2target1", BuildResultUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result2);

            var results = cache.ToArray();

            results.Length.ShouldBe(2);

            Assert.True(results[0].HasResultsForTarget("result1target1"));
            Assert.True(results[0].HasResultsForTarget("result1target2"));
            Assert.True(results[1].HasResultsForTarget("result2target1"));
        }

        [Fact]
        public void TestMissingResults()
        {
            ResultsCache cache = new ResultsCache();

            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget" }, null, BuildEventContext.Invalid, null);
            BuildResult retrievedResult = cache.GetResultForRequest(request);
            Assert.Null(retrievedResult);
        }

        [Fact]
        public void TestRetrieveMergedResults()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[2] { "testTarget", "testTarget2" }, null, BuildEventContext.Invalid, null);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", BuildResultUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            BuildResult result2 = new BuildResult(request);
            result2.AddResultsForTarget("testTarget2", BuildResultUtilities.GetEmptySucceedingTargetResult());
            cache.AddResult(result2);

            BuildResult retrievedResult = cache.GetResultForRequest(request);

            Assert.True(AreResultsIdenticalForTarget(result, retrievedResult, "testTarget"));
            Assert.True(AreResultsIdenticalForTarget(result2, retrievedResult, "testTarget2"));
        }

        [Fact]
        public void TestMergeResultsWithException()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[] { "testTarget" }, null, BuildEventContext.Invalid, null);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", BuildResultUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            BuildResult result2 = new BuildResult(request, new Exception("Test exception"));
            cache.AddResult(result2);

            BuildResult retrievedResult = cache.GetResultForRequest(request);

            Assert.NotNull(retrievedResult.Exception);
        }

        [Fact]
        public void TestRetrieveIncompleteResults()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ResultsCache cache = new ResultsCache();
                BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[2] { "testTarget", "testTarget2" }, null, BuildEventContext.Invalid, null);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("testTarget", BuildResultUtilities.GetEmptyFailingTargetResult());
                cache.AddResult(result);

                cache.GetResultForRequest(request);
            });
        }
        [Fact]
        public void TestRetrieveSubsetResults()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget2" }, null, BuildEventContext.Invalid, null);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", BuildResultUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            BuildResult result2 = new BuildResult(request);
            result2.AddResultsForTarget("testTarget2", BuildResultUtilities.GetEmptySucceedingTargetResult());
            cache.AddResult(result2);

            BuildResult retrievedResult = cache.GetResultForRequest(request);

            Assert.True(AreResultsIdenticalForTarget(result2, retrievedResult, "testTarget2"));
        }

        /// <summary>
        /// If a result had multiple targets associated with it and we only requested some of their
        /// results, the returned result should only contain the targets we asked for, and the overall
        /// status of the result should reflect the targets we asked for as well.
        /// </summary>
        [Fact]
        public void TestRetrieveSubsetTargetsFromResult()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget2" }, null, BuildEventContext.Invalid, null);

            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", BuildResultUtilities.GetEmptyFailingTargetResult());
            result.AddResultsForTarget("testTarget2", BuildResultUtilities.GetEmptySucceedingTargetResult());
            cache.AddResult(result);

            ResultsCacheResponse response = cache.SatisfyRequest(request, new List<string>(), new List<string>(new string[] { "testTarget2" }), skippedResultsDoNotCauseCacheMiss: false);

            Assert.Equal(ResultsCacheResponseType.Satisfied, response.Type);

            Assert.True(AreResultsIdenticalForTarget(result, response.Results, "testTarget2"));
            Assert.False(response.Results.HasResultsForTarget("testTarget"));
            Assert.Equal(BuildResultCode.Success, response.Results.OverallResult);
        }

        [Fact]
        public void TestCacheOnDifferentBuildFlagsPerRequest_ProvideProjectStateAfterBuild()
        {
            string targetName = "testTarget1";
            int submissionId = 1;
            int nodeRequestId = 0;
            int configurationId = 1;

            BuildRequest requestWithNoBuildDataFlags = new BuildRequest(
               submissionId,
               nodeRequestId,
               configurationId,
               new string[1] { targetName } /* escapedTargets */,
               null /* hostServices */,
               BuildEventContext.Invalid /* parentBuildEventContext */,
               null /* parentRequest */,
               BuildRequestDataFlags.None);

            BuildRequest requestWithProjectStateFlag = new BuildRequest(
               submissionId,
               nodeRequestId,
               configurationId,
               new string[1] { targetName } /* escapedTargets */,
               null /* hostServices */,
               BuildEventContext.Invalid /* parentBuildEventContext */,
               null /* parentRequest */,
               BuildRequestDataFlags.ProvideProjectStateAfterBuild);

            BuildRequest requestWithNoBuildDataFlags2 = new BuildRequest(
               submissionId,
               nodeRequestId,
               configurationId,
               new string[1] { targetName } /* escapedTargets */,
               null /* hostServices */,
               BuildEventContext.Invalid /* parentBuildEventContext */,
               null /* parentRequest */,
               BuildRequestDataFlags.None);

            BuildResult resultForRequestWithNoBuildDataFlags = new(requestWithNoBuildDataFlags);
            resultForRequestWithNoBuildDataFlags.AddResultsForTarget(targetName, BuildResultUtilities.GetEmptySucceedingTargetResult());
            ResultsCache cache = new();
            cache.AddResult(resultForRequestWithNoBuildDataFlags);

            ResultsCacheResponse cacheResponseForRequestWithNoBuildDataFlags = cache.SatisfyRequest(
               requestWithNoBuildDataFlags,
               new List<string>(),
               new List<string>(new string[] { targetName }),
               skippedResultsDoNotCauseCacheMiss: false);

            ResultsCacheResponse cachedResponseForProjectState = cache.SatisfyRequest(
               requestWithProjectStateFlag,
               new List<string>(),
               new List<string>(new string[] { targetName }),
               skippedResultsDoNotCauseCacheMiss: false);

            ResultsCacheResponse cacheResponseForNoBuildDataFlags2 = cache.SatisfyRequest(
               requestWithNoBuildDataFlags2,
               new List<string>(),
               new List<string>(new string[] { targetName }),
               skippedResultsDoNotCauseCacheMiss: false);

            Assert.Equal(ResultsCacheResponseType.Satisfied, cacheResponseForRequestWithNoBuildDataFlags.Type);

            // Because ProvideProjectStateAfterBuildFlag was provided as a part of BuildRequest
            Assert.Equal(ResultsCacheResponseType.NotSatisfied, cachedResponseForProjectState.Type);

            Assert.Equal(ResultsCacheResponseType.Satisfied, cacheResponseForNoBuildDataFlags2.Type);
        }

        [Fact]
        public void TestCacheOnDifferentBuildFlagsPerRequest_ProvideSubsetOfStateAfterBuild()
        {
            string targetName = "testTarget1";
            int submissionId = 1;
            int nodeRequestId = 0;
            int configurationId = 1;

            RequestedProjectState requestedProjectState1 = new()
            {
                PropertyFilters = ["property1", "property2"],
            };
            BuildRequest requestWithSubsetFlag1 = new BuildRequest(
                submissionId,
                nodeRequestId,
                configurationId,
                new string[1] { targetName } /* escapedTargets */,
                null /* hostServices */,
                BuildEventContext.Invalid /* parentBuildEventContext */,
                null /* parentRequest */,
                BuildRequestDataFlags.ProvideSubsetOfStateAfterBuild,
                requestedProjectState1);

            RequestedProjectState requestedProjectState2 = new()
            {
                PropertyFilters = ["property1"],
            };
            BuildRequest requestWithSubsetFlag2 = new BuildRequest(
                submissionId,
                nodeRequestId,
                configurationId,
                new string[1] { targetName } /* escapedTargets */,
                null /* hostServices */,
                BuildEventContext.Invalid /* parentBuildEventContext */,
                null /* parentRequest */,
                BuildRequestDataFlags.ProvideSubsetOfStateAfterBuild,
                requestedProjectState2);

            BuildResult resultForRequestWithSubsetFlag1 = new(requestWithSubsetFlag1);
            resultForRequestWithSubsetFlag1.AddResultsForTarget(targetName, BuildResultUtilities.GetEmptySucceedingTargetResult());

            using TextReader textReader = new StringReader(@"
              <Project>
                <PropertyGroup>
                  <property1>Value1</property1>
                  <property2>Value2</property2>
                </PropertyGroup>
              </Project>
            ");
            using XmlReader xmlReader = XmlReader.Create(textReader);
            resultForRequestWithSubsetFlag1.ProjectStateAfterBuild = new ProjectInstance(ProjectRootElement.Create(xmlReader)).FilteredCopy(requestedProjectState1);

            ResultsCache cache = new();
            cache.AddResult(resultForRequestWithSubsetFlag1);

            ResultsCacheResponse cachedResponseWithSubsetFlag1 = cache.SatisfyRequest(
                requestWithSubsetFlag1,
                new List<string>(),
                new List<string>(new string[] { targetName }),
                skippedResultsDoNotCauseCacheMiss: false);

            ResultsCacheResponse cachedResponseWithSubsetFlag2 = cache.SatisfyRequest(
                requestWithSubsetFlag2,
                new List<string>(),
                new List<string>(new string[] { targetName }),
                skippedResultsDoNotCauseCacheMiss: false);

            // We used the same filter that was used for the ProjectInstance in the cache -> cache hit.
            Assert.Equal(ResultsCacheResponseType.Satisfied, cachedResponseWithSubsetFlag1.Type);
            Assert.Equal("Value1", cachedResponseWithSubsetFlag1.Results.ProjectStateAfterBuild.GetPropertyValue("property1"));
            Assert.Equal("Value2", cachedResponseWithSubsetFlag1.Results.ProjectStateAfterBuild.GetPropertyValue("property2"));

            // We used a filter that's a subset of the one used for the ProjectInstance in the cache -> cache hit.
            Assert.Equal(ResultsCacheResponseType.Satisfied, cachedResponseWithSubsetFlag2.Type);
            Assert.Equal("Value1", cachedResponseWithSubsetFlag2.Results.ProjectStateAfterBuild.GetPropertyValue("property1"));
            Assert.Equal("", cachedResponseWithSubsetFlag2.Results.ProjectStateAfterBuild.GetPropertyValue("property2"));
        }

        [Fact]
        public void TestClearResultsCache()
        {
            ResultsCache cache = new ResultsCache();
            cache.ClearResults();

            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget2" }, null, BuildEventContext.Invalid, null);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", BuildResultUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            cache.ClearResults();

            Assert.Null(cache.GetResultForRequest(request));
        }

        public static IEnumerable<object[]> CacheSerializationTestData
        {
            get
            {
                yield return new[] { new ResultsCache() };

                var request1 = new BuildRequest(1, 2, 3, new[] { "target1" }, null, BuildEventContext.Invalid, null);
                var request2 = new BuildRequest(4, 5, 6, new[] { "target2" }, null, BuildEventContext.Invalid, null);

                var br1 = new BuildResult(request1);
                br1.AddResultsForTarget("target1", BuildResultUtilities.GetEmptySucceedingTargetResult());

                var resultsCache = new ResultsCache();
                resultsCache.AddResult(br1.Clone());

                yield return new[] { resultsCache };

                var br2 = new BuildResult(request2);
                br2.AddResultsForTarget("target2", BuildResultUtilities.GetEmptyFailingTargetResult());

                var resultsCache2 = new ResultsCache();
                resultsCache2.AddResult(br1.Clone());
                resultsCache2.AddResult(br2.Clone());

                yield return new[] { resultsCache2 };
            }
        }

        // Serialize latest version and deserialize latest version of the cache
        [Theory]
        [MemberData(nameof(CacheSerializationTestData))]
        public void TestResultsCacheTranslation(object obj)
        {
            var resultsCache = (ResultsCache)obj;

            resultsCache.Translate(TranslationHelpers.GetWriteTranslator());

            var copy = new ResultsCache(TranslationHelpers.GetReadTranslator());

            CompareResultsCache(resultsCache, copy);
        }

        [Theory]
        [InlineData(1, 1)] // Serialize version 0 and deserialize version 0 
        [InlineData(1, 0)] // Serialize version 0 and deserialize latest version
        public void TestResultsCacheTranslationAcrossVersions(int envValue1, int envValue2)
        {
            using (var env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDDONOTVERSIONBUILDRESULT", $"{envValue1}");

                // Create a ResultsCache
                var request1 = new BuildRequest(1, 2, 3, new[] { "target1" }, null, BuildEventContext.Invalid, null);
                var request2 = new BuildRequest(4, 5, 6, new[] { "target2" }, null, BuildEventContext.Invalid, null);

                var br1 = new BuildResult(request1);
                var br2 = new BuildResult(request2);
                br2.AddResultsForTarget("target2", BuildResultUtilities.GetEmptyFailingTargetResult());

                var resultsCache = new ResultsCache();
                resultsCache.AddResult(br1.Clone());
                resultsCache.AddResult(br2.Clone());

                resultsCache.Translate(TranslationHelpers.GetWriteTranslator());

                env.SetEnvironmentVariable("MSBUILDDONOTVERSIONBUILDRESULT", $"{envValue2}");
                Traits.UpdateFromEnvironment();

                var copy = new ResultsCache(TranslationHelpers.GetReadTranslator());

                CompareResultsCache(resultsCache, copy);
            }
        }

        private void CompareResultsCache(ResultsCache resultsCache1, ResultsCache resultsCache2)
        {
            resultsCache2.ResultsDictionary.Keys.ToHashSet().SetEquals(resultsCache1.ResultsDictionary.Keys.ToHashSet()).ShouldBeTrue();

            foreach (var configId in resultsCache2.ResultsDictionary.Keys)
            {
                var copiedBuildResult = resultsCache2.ResultsDictionary[configId];
                var initialBuildResult = resultsCache1.ResultsDictionary[configId];

                copiedBuildResult.SubmissionId.ShouldBe(initialBuildResult.SubmissionId);
                copiedBuildResult.ConfigurationId.ShouldBe(initialBuildResult.ConfigurationId);

                copiedBuildResult.ResultsByTarget.Keys.ToHashSet().SetEquals(initialBuildResult.ResultsByTarget.Keys.ToHashSet()).ShouldBeTrue();

                foreach (var targetKey in copiedBuildResult.ResultsByTarget.Keys)
                {
                    var copiedTargetResult = copiedBuildResult.ResultsByTarget[targetKey];
                    var initialTargetResult = initialBuildResult.ResultsByTarget[targetKey];

                    copiedTargetResult.WorkUnitResult.ResultCode.ShouldBe(initialTargetResult.WorkUnitResult.ResultCode);
                    copiedTargetResult.WorkUnitResult.ActionCode.ShouldBe(initialTargetResult.WorkUnitResult.ActionCode);
                }
            }
        }

        #region Helper Methods

        internal static bool AreResultsIdentical(BuildResult a, BuildResult b)
        {
            if (a.ConfigurationId != b.ConfigurationId)
            {
                return false;
            }

            if ((a.Exception == null) ^ (b.Exception == null))
            {
                return false;
            }

            if (a.Exception != null)
            {
                if (a.Exception.GetType() != b.Exception.GetType())
                {
                    return false;
                }
            }

            if (a.OverallResult != b.OverallResult)
            {
                return false;
            }

            foreach (KeyValuePair<string, TargetResult> targetResult in a.ResultsByTarget)
            {
                if (!AreResultsIdenticalForTarget(a, b, targetResult.Key))
                {
                    return false;
                }
            }

            foreach (KeyValuePair<string, TargetResult> targetResult in b.ResultsByTarget)
            {
                if (!AreResultsIdenticalForTarget(a, b, targetResult.Key))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool AreResultsIdenticalForTargets(BuildResult a, BuildResult b, string[] targets)
        {
            foreach (string target in targets)
            {
                if (!AreResultsIdenticalForTarget(a, b, target))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreResultsIdenticalForTarget(BuildResult a, BuildResult b, string target)
        {
            if (!a.HasResultsForTarget(target) || !b.HasResultsForTarget(target))
            {
                return false;
            }

            if (a[target].ResultCode != b[target].ResultCode)
            {
                return false;
            }

            if (!AreItemsIdentical(a[target].Items, b[target].Items))
            {
                return false;
            }

            return true;
        }

        private static bool AreItemsIdentical(IList<ITaskItem> a, IList<ITaskItem> b)
        {
            // Exhaustive comparison of items should not be necessary since we don't merge on the item level.
            return a.Count == b.Count;
        }

        #endregion
    }
}
