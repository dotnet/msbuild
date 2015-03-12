// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Microsoft.Build.Unittest;

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestClass]
    public class BuildResult_Tests
    {
        private int _nodeRequestId;

        [TestInitialize]
        public void SetUp()
        {
            _nodeRequestId = 1;
        }

        [TestCleanup]
        public void TearDown()
        {
        }

        [TestMethod]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result2 = new BuildResult(request);
        }

        [TestMethod]
        public void Clone()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result1 = new BuildResult(request);
            result1.ResultsByTarget.Add("FOO", TestUtilities.GetEmptySucceedingTargetResult());
            Assert.IsTrue(result1.ResultsByTarget.ContainsKey("foo")); // test comparer

            BuildResult result2 = result1.Clone();

            result1.ResultsByTarget.Add("BAR", TestUtilities.GetEmptySucceedingTargetResult());
            Assert.IsTrue(result1.ResultsByTarget.ContainsKey("foo")); // test comparer
            Assert.IsTrue(result1.ResultsByTarget.ContainsKey("bar"));

            Assert.AreEqual(result1.SubmissionId, result2.SubmissionId);
            Assert.AreEqual(result1.ConfigurationId, result2.ConfigurationId);
            Assert.AreEqual(result1.GlobalRequestId, result2.GlobalRequestId);
            Assert.AreEqual(result1.ParentGlobalRequestId, result2.ParentGlobalRequestId);
            Assert.AreEqual(result1.NodeRequestId, result2.NodeRequestId);
            Assert.AreEqual(result1.CircularDependency, result2.CircularDependency);
            Assert.AreEqual(result1.ResultsByTarget["foo"], result2.ResultsByTarget["foo"]);
            Assert.AreEqual(result1.OverallResult, result2.OverallResult);
        }

        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestConstructorBad()
        {
            BuildResult result = new BuildResult(null);
        }

        [TestMethod]
        public void TestConfigurationId()
        {
            BuildRequest request = CreateNewBuildRequest(-1, new string[0]);
            BuildResult result = new BuildResult(request);
            Assert.AreEqual(-1, result.ConfigurationId);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[0]);
            BuildResult result2 = new BuildResult(request2);
            Assert.AreEqual(1, result2.ConfigurationId);
        }

        [TestMethod]
        public void TestExceptionGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            Assert.IsNull(result.Exception);

            AccessViolationException e = new AccessViolationException();
            result = new BuildResult(request, e);

            Assert.AreEqual(e, result.Exception);
        }

        [TestMethod]
        public void TestOverallResult()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            Assert.AreEqual(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            Assert.AreEqual(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("bar", new TargetResult(new TaskItem[0] { }, new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, new Exception())));
            Assert.AreEqual(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("baz", new TargetResult(new TaskItem[0] { }, TestUtilities.GetStopWithErrorResult(new Exception())));
            Assert.AreEqual(BuildResultCode.Failure, result.OverallResult);

            BuildRequest request2 = CreateNewBuildRequest(2, new string[0]);
            BuildResult result2 = new BuildResult(request2);
            result2.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            result2.AddResultsForTarget("bar", TestUtilities.GetEmptyFailingTargetResult());
            Assert.AreEqual(BuildResultCode.Failure, result2.OverallResult);
        }

        [TestMethod]
        public void TestPacketType()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            Assert.AreEqual(NodePacketType.BuildResult, ((INodePacket)result).Type);
        }

        [TestMethod]
        public void TestAddAndRetrieve()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            result.AddResultsForTarget("bar", TestUtilities.GetEmptyFailingTargetResult());

            Assert.AreEqual(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.AreEqual(TargetResultCode.Failure, result["bar"].ResultCode);
        }

        [ExpectedException(typeof(KeyNotFoundException))]
        [TestMethod]
        public void TestIndexerBad1()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            ITargetResult targetResult = result["foo"];
        }

        [ExpectedException(typeof(KeyNotFoundException))]
        [TestMethod]
        public void TestIndexerBad2()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            ITargetResult targetResult = result["bar"];
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TestAddResultsInvalid1()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget(null, TestUtilities.GetEmptySucceedingTargetResult());
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TestAddResultsInvalid2()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", null);
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TestAddResultsInvalid3()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget(null, TestUtilities.GetEmptySucceedingTargetResult());
        }

        [TestMethod]
        public void TestMergeResults()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());

            BuildResult result2 = new BuildResult(request);
            result.AddResultsForTarget("bar", TestUtilities.GetEmptyFailingTargetResult());

            result.MergeResults(result2);
            Assert.AreEqual(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.AreEqual(TargetResultCode.Failure, result["bar"].ResultCode);

            BuildResult result3 = new BuildResult(request);
            result.MergeResults(result3);

            BuildResult result4 = new BuildResult(request);
            result4.AddResultsForTarget("xor", TestUtilities.GetEmptySucceedingTargetResult());
            result.MergeResults(result4);
            Assert.AreEqual(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.AreEqual(TargetResultCode.Failure, result["bar"].ResultCode);
            Assert.AreEqual(TargetResultCode.Success, result["xor"].ResultCode);
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TestMergeResultsBad1()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());

            result.MergeResults(null);
        }

        // See the implementation of BuildResult.MergeResults for an explanation of why this
        // test is disabled.
#if false
        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestMergeResultsBad2()
        {
            BuildResult result = new BuildResult(1);
            result["foo"] = new TargetResult(new BuildItem[0] { }, BuildResultCode.Success);

            BuildResult result2 = new BuildResult(1);
            result2["foo"] = new TargetResult(new BuildItem[0] { }, BuildResultCode.Success);

            result.MergeResults(result2);
        }
#endif

        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestMergeResultsBad3()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());

            BuildRequest request2 = CreateNewBuildRequest(2, new string[0]);
            BuildResult result2 = new BuildResult(request2);
            result2.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());

            result.MergeResults(result2);
        }

        [TestMethod]
        public void TestHasResultsForTarget()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());

            Assert.IsTrue(result.HasResultsForTarget("foo"));
            Assert.IsFalse(result.HasResultsForTarget("bar"));
        }

        [TestMethod]
        public void TestEnumerator()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            int countFound = 0;
            foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
            {
                countFound++;
            }
            Assert.AreEqual(countFound, 0);

            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            bool foundFoo = false;
            countFound = 0;
            foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
            {
                if (resultPair.Key == "foo")
                {
                    foundFoo = true;
                }
                countFound++;
            }
            Assert.AreEqual(countFound, 1);
            Assert.IsTrue(foundFoo);

            result.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            foundFoo = false;
            bool foundBar = false;
            countFound = 0;
            foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
            {
                if (resultPair.Key == "foo")
                {
                    Assert.IsFalse(foundFoo);
                    foundFoo = true;
                }
                if (resultPair.Key == "bar")
                {
                    Assert.IsFalse(foundBar);
                    foundBar = true;
                }
                countFound++;
            }
            Assert.AreEqual(countFound, 2);
            Assert.IsTrue(foundFoo);
            Assert.IsTrue(foundBar);
        }


        [TestMethod]
        public void TestTranslation()
        {
            BuildRequest request = new BuildRequest(1, 1, 2, new string[] { "alpha", "omega" }, null, new BuildEventContext(1, 1, 2, 3, 4, 5), null);
            BuildResult result = new BuildResult(request, new BuildAbortedException());

            TaskItem fooTaskItem = new TaskItem("foo", "asdf.proj");
            fooTaskItem.SetMetadata("meta1", "metavalue1");
            fooTaskItem.SetMetadata("meta2", "metavalue2");

            result.InitialTargets = new List<string> { "a", "b" };
            result.DefaultTargets = new List<string> { "c", "d" };

            result.AddResultsForTarget("alpha", new TargetResult(new TaskItem[] { fooTaskItem }, TestUtilities.GetSuccessResult()));
            result.AddResultsForTarget("omega", new TargetResult(new TaskItem[] { }, TestUtilities.GetStopWithErrorResult(new ArgumentException("The argument was invalid"))));

            Assert.AreEqual(NodePacketType.BuildResult, (result as INodePacket).Type);
            ((INodePacketTranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildResult deserializedResult = packet as BuildResult;

            Assert.AreEqual(result.ConfigurationId, deserializedResult.ConfigurationId);
            Assert.IsTrue(TranslationHelpers.CompareCollections(result.DefaultTargets, deserializedResult.DefaultTargets, StringComparer.Ordinal));
            Assert.IsTrue(TranslationHelpers.CompareExceptions(result.Exception, deserializedResult.Exception));
            Assert.AreEqual(result.Exception.Message, deserializedResult.Exception.Message);
            Assert.AreEqual(result.GlobalRequestId, deserializedResult.GlobalRequestId);
            Assert.IsTrue(TranslationHelpers.CompareCollections(result.InitialTargets, deserializedResult.InitialTargets, StringComparer.Ordinal));
            Assert.AreEqual(result.NodeRequestId, deserializedResult.NodeRequestId);
            Assert.AreEqual(result["alpha"].ResultCode, deserializedResult["alpha"].ResultCode);
            Assert.IsTrue(TranslationHelpers.CompareExceptions(result["alpha"].Exception, deserializedResult["alpha"].Exception));
            Assert.IsTrue(TranslationHelpers.CompareCollections(result["alpha"].Items, deserializedResult["alpha"].Items, TaskItemComparer.Instance));
            Assert.AreEqual(result["omega"].ResultCode, deserializedResult["omega"].ResultCode);
            Assert.IsTrue(TranslationHelpers.CompareExceptions(result["omega"].Exception, deserializedResult["omega"].Exception));
            Assert.IsTrue(TranslationHelpers.CompareCollections(result["omega"].Items, deserializedResult["omega"].Items, TaskItemComparer.Instance));
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}
