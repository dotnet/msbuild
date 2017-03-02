// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Unittest;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class BuildRequestEntry_Tests
    {
        private int _nodeRequestId;

        [Fact]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0] { });
            BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);

            Assert.Equal(entry.State, BuildRequestEntryState.Ready);
            Assert.Equal(entry.Request, request);
        }

        [Fact]
        public void TestConstructorBad()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequestEntry entry = new BuildRequestEntry(null, null);
            }
           );
        }
        [Fact]
        public void TestSimpleStateProgression()
        {
            // Start in Ready
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.Equal(entry.State, BuildRequestEntryState.Ready);
            Assert.Equal(entry.Request, request);
            Assert.Null(entry.Result);

            // Move to active.  Should not be any results yet.
            IDictionary<int, BuildResult> results = entry.Continue();
            Assert.Equal(entry.State, BuildRequestEntryState.Active);
            Assert.Null(entry.Result);
            Assert.Null(results);

            // Wait for results, move to waiting.
            BuildRequest waitingRequest = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);
            Assert.Equal(entry.Request, request);
            Assert.Null(entry.Result);

            // Provide the results, move to ready.
            BuildResult requiredResult = new BuildResult(waitingRequest);
            requiredResult.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.Equal(entry.State, BuildRequestEntryState.Ready);
            Assert.Equal(entry.Request, request);
            Assert.Null(entry.Result);

            // Continue the build, move to active.
            results = entry.Continue();
            Assert.Equal(entry.State, BuildRequestEntryState.Active);
            Assert.Null(entry.Result);
            Assert.Equal(results.Count, 1);
            Assert.True(results.ContainsKey(requiredResult.NodeRequestId));
            Assert.Equal(results[requiredResult.NodeRequestId], requiredResult);

            // Complete the build, move to completed.
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            entry.Complete(result);
            Assert.Equal(entry.State, BuildRequestEntryState.Complete);
            Assert.NotNull(entry.Result);
            Assert.Equal(entry.Result, result);
        }

        [Fact]
        public void TestResolveConfiguration()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);

            entry.Continue();
            Assert.Equal(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest = CreateNewBuildRequest(-1, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest);

            entry.ResolveConfigurationRequest(-1, 2);

            BuildResult requiredResult = new BuildResult(waitingRequest);
            requiredResult.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.Equal(entry.State, BuildRequestEntryState.Ready);
        }

        [Fact]
        public void TestMultipleWaitingRequests()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);

            entry.Continue();
            Assert.Equal(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

            BuildRequest waitingRequest2 = CreateNewBuildRequest(2, new string[1] { "xor" });
            entry.WaitForResult(waitingRequest2);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult1 = new BuildResult(waitingRequest1);
            requiredResult1.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult1);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult2 = new BuildResult(waitingRequest2);
            requiredResult2.AddResultsForTarget("xor", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult2);
            Assert.Equal(entry.State, BuildRequestEntryState.Ready);
        }

        [Fact]
        public void TestMixedWaitingRequests()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.Equal(entry.State, BuildRequestEntryState.Ready);

            entry.Continue();
            Assert.Equal(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

            BuildRequest waitingRequest2 = CreateNewBuildRequest(-1, new string[1] { "xor" });
            entry.WaitForResult(waitingRequest2);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

            Assert.Null(entry.GetRequestsToIssueIfReady()); // "Entry should not be ready to issue because there are unresolved configurations"

            entry.ResolveConfigurationRequest(-1, 3);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult1 = new BuildResult(waitingRequest1);
            requiredResult1.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult1);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult2 = new BuildResult(waitingRequest2);
            requiredResult2.AddResultsForTarget("xor", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult2);
            Assert.Equal(entry.State, BuildRequestEntryState.Ready);
        }

        [Fact]
        public void TestNoReadyToWaiting()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
                BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
                BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
                BuildRequestEntry entry = new BuildRequestEntry(request, config);
                Assert.Equal(entry.State, BuildRequestEntryState.Ready);

                BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
                entry.WaitForResult(waitingRequest1);
            }
           );
        }

        [Fact]
        public void TestNoReadyToComplete()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
                BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
                BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
                BuildRequestEntry entry = new BuildRequestEntry(request, config);
                Assert.Equal(entry.State, BuildRequestEntryState.Ready);

                BuildResult requiredResult = new BuildResult(request);
                requiredResult.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
                entry.Complete(requiredResult);
            }
           );
        }

        [Fact]
        public void TestNoWaitingToComplete()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
                BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
                BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
                BuildRequestEntry entry = new BuildRequestEntry(request, config);
                Assert.Equal(entry.State, BuildRequestEntryState.Ready);

                entry.Continue();
                Assert.Equal(entry.State, BuildRequestEntryState.Active);

                BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
                entry.WaitForResult(waitingRequest1);
                Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

                BuildResult requiredResult = new BuildResult(request);
                requiredResult.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
                entry.Complete(requiredResult);
            }
           );
        }

        [Fact]
        public void TestNoCompleteToWaiting()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
                BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null), "2.0");
                BuildRequestEntry entry = new BuildRequestEntry(request, config);
                Assert.Equal(entry.State, BuildRequestEntryState.Ready);

                entry.Continue();
                Assert.Equal(entry.State, BuildRequestEntryState.Active);

                BuildResult requiredResult = new BuildResult(request);
                requiredResult.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
                entry.Complete(requiredResult);
                Assert.Equal(entry.State, BuildRequestEntryState.Complete);

                BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
                entry.WaitForResult(waitingRequest1);
            }
           );
        }
        [Fact]
        public void TestResultsWithNoMatch1()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.Equal(entry.State, BuildRequestEntryState.Ready);

            entry.Continue();
            Assert.Equal(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);

            BuildRequest randomRequest = CreateNewBuildRequest(3, new string[0]);
            BuildResult requiredResult = new BuildResult(randomRequest);
            requiredResult.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.Equal(entry.State, BuildRequestEntryState.Waiting);
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}
