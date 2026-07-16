// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Unittest;

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestClass]
    public class BuildRequestEntry_Tests
    {
        private int _nodeRequestId;

        /// <summary>
        /// Creates a stub TaskEnvironment for testing purposes.
        /// </summary>
        private static TaskEnvironment CreateStubTaskEnvironment() => TaskEnvironmentHelper.CreateForTest();

        [MSBuildTestMethod]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());

            Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);
            Assert.AreEqual(entry.Request, request);
        }

        [MSBuildTestMethod]
        public void TestConstructorBad()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequestEntry entry = new BuildRequestEntry(null!, null!, null!);
            });
        }
        [MSBuildTestMethod]
        public void TestSimpleStateProgression()
        {
            // Start in Ready
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());
            Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);
            Assert.AreEqual(entry.Request, request);
            Assert.IsNull(entry.Result);

            // Move to active.  Should not be any results yet.
            IDictionary<int, BuildResult> results = entry.Continue();
            Assert.AreEqual(BuildRequestEntryState.Active, entry.State);
            Assert.IsNull(entry.Result);
            Assert.IsNull(results);

            // Wait for results, move to waiting.
            BuildRequest waitingRequest = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);
            Assert.AreEqual(entry.Request, request);
            Assert.IsNull(entry.Result);

            // Provide the results, move to ready.
            BuildResult requiredResult = new BuildResult(waitingRequest);
            requiredResult.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);
            Assert.AreEqual(entry.Request, request);
            Assert.IsNull(entry.Result);

            // Continue the build, move to active.
            results = entry.Continue();
            Assert.AreEqual(BuildRequestEntryState.Active, entry.State);
            Assert.IsNull(entry.Result);
            Assert.ContainsSingle(results);
            Assert.IsTrue(results.ContainsKey(requiredResult.NodeRequestId));
            Assert.AreEqual(results[requiredResult.NodeRequestId], requiredResult);

            // Complete the build, move to completed.
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            entry.Complete(result);
            Assert.AreEqual(BuildRequestEntryState.Complete, entry.State);
            Assert.IsNotNull(entry.Result);
            Assert.AreEqual(entry.Result, result);
        }

        [MSBuildTestMethod]
        public void TestResolveConfiguration()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());

            entry.Continue();
            Assert.AreEqual(BuildRequestEntryState.Active, entry.State);

            BuildRequest waitingRequest = CreateNewBuildRequest(-1, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest);

            entry.ResolveConfigurationRequest(-1, 2);

            BuildResult requiredResult = new BuildResult(waitingRequest);
            requiredResult.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);
        }

        [MSBuildTestMethod]
        public void TestMultipleWaitingRequests()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());

            entry.Continue();
            Assert.AreEqual(BuildRequestEntryState.Active, entry.State);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

            BuildRequest waitingRequest2 = CreateNewBuildRequest(2, new string[1] { "xor" });
            entry.WaitForResult(waitingRequest2);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

            BuildResult requiredResult1 = new BuildResult(waitingRequest1);
            requiredResult1.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult1);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

            BuildResult requiredResult2 = new BuildResult(waitingRequest2);
            requiredResult2.AddResultsForTarget("xor", BuildResultUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult2);
            Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);
        }

        [MSBuildTestMethod]
        public void TestMixedWaitingRequests()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());
            Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);

            entry.Continue();
            Assert.AreEqual(BuildRequestEntryState.Active, entry.State);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

            BuildRequest waitingRequest2 = CreateNewBuildRequest(-1, new string[1] { "xor" });
            entry.WaitForResult(waitingRequest2);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

            Assert.IsNull(entry.GetRequestsToIssueIfReady()); // "Entry should not be ready to issue because there are unresolved configurations"

            entry.ResolveConfigurationRequest(-1, 3);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

            BuildResult requiredResult1 = new BuildResult(waitingRequest1);
            requiredResult1.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult1);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

            BuildResult requiredResult2 = new BuildResult(waitingRequest2);
            requiredResult2.AddResultsForTarget("xor", BuildResultUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult2);
            Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);
        }

        [MSBuildTestMethod]
        public void TestNoReadyToWaiting()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
                BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null);
                BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
                BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());
                Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);

                BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
                entry.WaitForResult(waitingRequest1);
            });
        }

        [MSBuildTestMethod]
        public void TestNoReadyToComplete()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
                BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null);
                BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
                BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());
                Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);

                BuildResult requiredResult = new BuildResult(request);
                requiredResult.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
                entry.Complete(requiredResult);
            });
        }

        [MSBuildTestMethod]
        public void TestNoWaitingToComplete()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
                BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null);
                BuildRequestConfiguration config = new BuildRequestConfiguration(1, data1, "2.0");
                BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());
                Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);

                entry.Continue();
                Assert.AreEqual(BuildRequestEntryState.Active, entry.State);

                BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
                entry.WaitForResult(waitingRequest1);
                Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

                BuildResult requiredResult = new BuildResult(request);
                requiredResult.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
                entry.Complete(requiredResult);
            });
        }

        [MSBuildTestMethod]
        public void TestNoCompleteToWaiting()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
                BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null), "2.0");
                BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());
                Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);

                entry.Continue();
                Assert.AreEqual(BuildRequestEntryState.Active, entry.State);

                BuildResult requiredResult = new BuildResult(request);
                requiredResult.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
                entry.Complete(requiredResult);
                Assert.AreEqual(BuildRequestEntryState.Complete, entry.State);

                BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
                entry.WaitForResult(waitingRequest1);
            });
        }
        [MSBuildTestMethod]
        public void TestResultsWithNoMatch1()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "foo" });
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, new BuildRequestData("foo", new Dictionary<string, string?>(), "foo", Array.Empty<string>(), null), "2.0");
            BuildRequestEntry entry = new BuildRequestEntry(request, config, CreateStubTaskEnvironment());
            Assert.AreEqual(BuildRequestEntryState.Ready, entry.State);

            entry.Continue();
            Assert.AreEqual(BuildRequestEntryState.Active, entry.State);

            BuildRequest waitingRequest1 = CreateNewBuildRequest(2, new string[1] { "bar" });
            entry.WaitForResult(waitingRequest1);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);

            BuildRequest randomRequest = CreateNewBuildRequest(3, Array.Empty<string>());
            BuildResult requiredResult = new BuildResult(randomRequest);
            requiredResult.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());
            entry.ReportResult(requiredResult);
            Assert.AreEqual(BuildRequestEntryState.Waiting, entry.State);
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}
