// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Unittest;
using Xunit;



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
            result.AddResultsForTarget("testTarget", TestUtilities.GetEmptyFailingTargetResult());
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
            result.AddResultsForTarget("testTarget", TestUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "otherTarget" }, null, BuildEventContext.Invalid, null);
            result = new BuildResult(request);
            result.AddResultsForTarget("otherTarget", TestUtilities.GetEmptySucceedingTargetResult());
            cache.AddResult(result);

            BuildResult retrievedResult = cache.GetResultsForConfiguration(1);

            Assert.True(retrievedResult.HasResultsForTarget("testTarget"));
            Assert.True(retrievedResult.HasResultsForTarget("otherTarget"));
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
            result.AddResultsForTarget("testTarget", TestUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            BuildResult result2 = new BuildResult(request);
            result2.AddResultsForTarget("testTarget2", TestUtilities.GetEmptySucceedingTargetResult());
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
            result.AddResultsForTarget("testTarget", TestUtilities.GetEmptyFailingTargetResult());
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
                result.AddResultsForTarget("testTarget", TestUtilities.GetEmptyFailingTargetResult());
                cache.AddResult(result);

                BuildResult retrievedResult = cache.GetResultForRequest(request);
            }
           );
        }
        [Fact]
        public void TestRetrieveSubsetResults()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget2" }, null, BuildEventContext.Invalid, null);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", TestUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            BuildResult result2 = new BuildResult(request);
            result2.AddResultsForTarget("testTarget2", TestUtilities.GetEmptySucceedingTargetResult());
            cache.AddResult(result2);

            BuildResult retrievedResult = cache.GetResultForRequest(request);

            Assert.True(AreResultsIdenticalForTarget(result2, retrievedResult, "testTarget2"));
        }

        /// <summary>
        /// If a result had multiple targets associated with it and we only requested some of their 
        /// results, the returned result should only contain the targets we asked for, BUT the overall 
        /// status of the result should remain the same.  
        /// </summary>
        [Fact]
        public void TestRetrieveSubsetTargetsFromResult()
        {
            ResultsCache cache = new ResultsCache();
            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget2" }, null, BuildEventContext.Invalid, null);

            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", TestUtilities.GetEmptyFailingTargetResult());
            result.AddResultsForTarget("testTarget2", TestUtilities.GetEmptySucceedingTargetResult());
            cache.AddResult(result);

            ResultsCacheResponse response = cache.SatisfyRequest(request, new List<string>(), new List<string>(new string[] { "testTarget2" }), new List<string>(new string[] { "testTarget" }), skippedResultsAreOK: false);

            Assert.Equal(ResultsCacheResponseType.Satisfied, response.Type);

            Assert.True(AreResultsIdenticalForTarget(result, response.Results, "testTarget2"));
            Assert.False(response.Results.HasResultsForTarget("testTarget"));
            Assert.Equal(BuildResultCode.Failure, response.Results.OverallResult);
        }

        [Fact]
        public void TestClearResultsCache()
        {
            ResultsCache cache = new ResultsCache();
            cache.ClearResults();

            BuildRequest request = new BuildRequest(1 /* submissionId */, 0, 1, new string[1] { "testTarget2" }, null, BuildEventContext.Invalid, null);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("testTarget", TestUtilities.GetEmptyFailingTargetResult());
            cache.AddResult(result);

            cache.ClearResults();

            Assert.Null(cache.GetResultForRequest(request));
        }

        #region Helper Methods

        static internal bool AreResultsIdentical(BuildResult a, BuildResult b)
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

        static internal bool AreResultsIdenticalForTargets(BuildResult a, BuildResult b, string[] targets)
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

        static private bool AreResultsIdenticalForTarget(BuildResult a, BuildResult b, string target)
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

        static private bool AreItemsIdentical(IList<ITaskItem> a, IList<ITaskItem> b)
        {
            // Exhaustive comparison of items should not be necessary since we don't merge on the item level.
            return a.Count == b.Count;
        }

        #endregion
    }
}
