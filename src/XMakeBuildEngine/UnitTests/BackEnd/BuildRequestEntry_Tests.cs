// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Microsoft.Build.Unittest;

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestClass]
    public class BuildRequestEntry_Tests
    {
        private int _nodeRequestId;

        [TestInitialize]
        public void SetUp()
        {
            _nodeRequestId++;
        }

        [TestCleanup]
        public void TearDown()
        {
        }

        [TestMethod]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0] { });
            BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);

            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);
            Assert.AreEqual(entry.Request, request);
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TestConstructorBad()
        {
            BuildRequestEntry entry = new BuildRequestEntry(null, null);
        }

        [TestMethod]
        public void TestSimpleStateProgression()
        {
            // Start in Ready
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);
            Assert.AreEqual(entry.Request, request);
            Assert.IsNull(entry.Result);

            // Move to active.  Should not be any results yet.
            IDictionary<int, BuildResult> results = entry.Continue();
            Assert.AreEqual(entry.State, BuildRequestEntryState.Active);
            Assert.IsNull(entry.Result);
            Assert.IsNull(results);

            // Wait for results, move to waiting.
            BuildRequest waitingRequest = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);
            Assert.AreEqual(entry.Request, request);
            Assert.IsNull(entry.Result);

            // Provide the results, move to ready.
            BuildResult requiredResult = new BuildResult(waitingRequest);
            requiredResult.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);
            Assert.AreEqual(entry.Request, request);
            Assert.IsNull(entry.Result);

            // Continue the build, move to active.
            results = entry.Continue();
            Assert.AreEqual(entry.State, BuildRequestEntryState.Active);
            Assert.IsNull(entry.Result);
            Assert.AreEqual(results.Count, 1);
            Assert.IsTrue(results.ContainsKey(requiredResult.NodeRequestId));
            Assert.AreEqual(results[requiredResult.NodeRequestId], requiredResult);

            // Complete the build, move to completed.
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            entry.Complete(result);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Complete);
            Assert.IsNotNull(entry.Result);
            Assert.AreEqual(entry.Result, result);
        }

        [TestMethod]
        public void TestResolveConfiguration()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);

            entry.Continue();
            Assert.AreEqual(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest = CreateNewBuildRequest(-1, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest);

            entry.ResolveConfigurationRequest(-1, 2);

            BuildResult requiredResult = new BuildResult(waitingRequest);
            requiredResult.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);
        }

        [TestMethod]
        public void TestMultipleWaitingRequests()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);

            entry.Continue();
            Assert.AreEqual(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            BuildRequest waitingRequest2 = CreateNewBuildRequest(2, new string[1] { "xor" });
            entry.WaitForResult(waitingRequest2);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult1 = new BuildResult(waitingRequest1);
            requiredResult1.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult1);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult2 = new BuildResult(waitingRequest2);
            requiredResult2.AddResultsForTarget("xor", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult2);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);
        }

        [TestMethod]
        public void TestMixedWaitingRequests()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);

            entry.Continue();
            Assert.AreEqual(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            BuildRequest waitingRequest2 = CreateNewBuildRequest(-1, new string[1] { "xor" });
            entry.WaitForResult(waitingRequest2);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            Assert.IsNull(entry.GetRequestsToIssueIfReady(), "Entry should not be ready to issue because there are unresolved configurations");

            entry.ResolveConfigurationRequest(-1, 3);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult1 = new BuildResult(waitingRequest1);
            requiredResult1.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult1);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult2 = new BuildResult(waitingRequest2);
            requiredResult2.AddResultsForTarget("xor", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult2);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);
        }

        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestNoReadyToWaiting()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
        }

        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestNoReadyToComplete()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);

            BuildResult requiredResult = new BuildResult(request);
            requiredResult.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            entry.Complete(requiredResult);
        }

        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestNoWaitingToComplete()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);

            entry.Continue();
            Assert.AreEqual(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            BuildResult requiredResult = new BuildResult(request);
            requiredResult.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            entry.Complete(requiredResult);
        }

        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestNoCompleteToWaiting()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);

            entry.Continue();
            Assert.AreEqual(entry.State, BuildRequestEntryState.Active);

            BuildResult requiredResult = new BuildResult(request);
            requiredResult.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            entry.Complete(requiredResult);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Complete);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
        }

        [TestMethod]
        public void TestResultsWithNoMatch1()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string>(), "foo", new string[0], null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Ready);

            entry.Continue();
            Assert.AreEqual(entry.State, BuildRequestEntryState.Active);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);

            BuildRequest randomRequest = CreateNewBuildRequest(3, new string[0]);
            BuildResult requiredResult = new BuildResult(randomRequest);
            requiredResult.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.AreEqual(entry.State, BuildRequestEntryState.Waiting);
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}
