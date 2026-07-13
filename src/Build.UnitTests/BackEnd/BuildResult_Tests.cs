// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Engine.UnitTests.TestComparers;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Unittest;
using Shouldly;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestClass]
    public class BuildResult_Tests
    {
        private int _nodeRequestId;

        public BuildResult_Tests()
        {
            _nodeRequestId = 1;
        }

        [MSBuildTestMethod]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result2 = new BuildResult(request);
        }

        [MSBuildTestMethod]
        public void Clone()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result1 = new BuildResult(request);
            result1.ResultsByTarget?.Add("FOO", BuildResultUtilities.GetEmptySucceedingTargetResult());
            Assert.IsTrue(result1.ResultsByTarget?.ContainsKey("foo")); // test comparer

            BuildResult result2 = result1.Clone();

            result1.ResultsByTarget?.Add("BAR", BuildResultUtilities.GetEmptySucceedingTargetResult());
            Assert.IsTrue(result1.ResultsByTarget?.ContainsKey("foo")); // test comparer
            Assert.IsTrue(result1.ResultsByTarget?.ContainsKey("bar"));

            Assert.AreEqual(result1.SubmissionId, result2.SubmissionId);
            Assert.AreEqual(result1.ConfigurationId, result2.ConfigurationId);
            Assert.AreEqual(result1.GlobalRequestId, result2.GlobalRequestId);
            Assert.AreEqual(result1.ParentGlobalRequestId, result2.ParentGlobalRequestId);
            Assert.AreEqual(result1.NodeRequestId, result2.NodeRequestId);
            Assert.AreEqual(result1.CircularDependency, result2.CircularDependency);
            Assert.AreEqual(result1.ResultsByTarget?["foo"], result2.ResultsByTarget?["foo"]);
            Assert.AreEqual(result1.OverallResult, result2.OverallResult);
        }

        [MSBuildTestMethod]
        public void TestConstructorBad()
        {
            Assert.ThrowsExactly<NullReferenceException>(() =>
            {
                BuildResult result = new BuildResult(null!);
            });
        }
        [MSBuildTestMethod]
        public void TestConfigurationId()
        {
            BuildRequest request = CreateNewBuildRequest(-1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            Assert.AreEqual(-1, result.ConfigurationId);

            BuildRequest request2 = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result2 = new BuildResult(request2);
            Assert.AreEqual(1, result2.ConfigurationId);
        }

        [MSBuildTestMethod]
        public void TestExceptionGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            Assert.IsNull(result.Exception);
            AccessViolationException e = new AccessViolationException();
            result = new BuildResult(request, e);

            Assert.AreEqual(e, result.Exception);
        }

        [MSBuildTestMethod]
        public void TestOverallResult()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            Assert.AreEqual(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            Assert.AreEqual(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("bar", new TargetResult(Array.Empty<TaskItem>(), new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, new Exception())));
            Assert.AreEqual(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("baz", new TargetResult(Array.Empty<TaskItem>(), BuildResultUtilities.GetStopWithErrorResult(new Exception())));
            Assert.AreEqual(BuildResultCode.Failure, result.OverallResult);

            BuildRequest request2 = CreateNewBuildRequest(2, Array.Empty<string>());
            BuildResult result2 = new BuildResult(request2);
            result2.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            result2.AddResultsForTarget("bar", BuildResultUtilities.GetEmptyFailingTargetResult());
            Assert.AreEqual(BuildResultCode.Failure, result2.OverallResult);
        }

        [MSBuildTestMethod]
        public void TestPacketType()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            Assert.AreEqual(NodePacketType.BuildResult, ((INodePacket)result).Type);
        }

        [MSBuildTestMethod]
        public void TestAddAndRetrieve()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            result.AddResultsForTarget("bar", BuildResultUtilities.GetEmptyFailingTargetResult());

            Assert.AreEqual(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.AreEqual(TargetResultCode.Failure, result["bar"].ResultCode);
        }

        [MSBuildTestMethod]
        public void TestIndexerBad1()
        {
            Assert.ThrowsExactly<KeyNotFoundException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                ITargetResult targetResult = result["foo"];
            });
        }

        [MSBuildTestMethod]
        public void TestIndexerBad2()
        {
            Assert.ThrowsExactly<KeyNotFoundException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
                ITargetResult targetResult = result["bar"];
            });
        }

        [MSBuildTestMethod]
        public void TestAddResultsInvalid1()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget(null!, BuildResultUtilities.GetEmptySucceedingTargetResult());
            });
        }

        [MSBuildTestMethod]
        public void TestAddResultsInvalid2()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", null!);
            });
        }

        [MSBuildTestMethod]
        public void TestAddResultsInvalid3()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget(null!, BuildResultUtilities.GetEmptySucceedingTargetResult());
            });
        }
        [MSBuildTestMethod]
        public void TestMergeResults()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());

            BuildResult result2 = new BuildResult(request);
            result.AddResultsForTarget("bar", BuildResultUtilities.GetEmptyFailingTargetResult());

            result.MergeResults(result2);
            Assert.AreEqual(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.AreEqual(TargetResultCode.Failure, result["bar"].ResultCode);

            BuildResult result3 = new BuildResult(request);
            result.MergeResults(result3);

            BuildResult result4 = new BuildResult(request);
            result4.AddResultsForTarget("xor", BuildResultUtilities.GetEmptySucceedingTargetResult());
            result.MergeResults(result4);
            Assert.AreEqual(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.AreEqual(TargetResultCode.Failure, result["bar"].ResultCode);
            Assert.AreEqual(TargetResultCode.Success, result["xor"].ResultCode);
        }

        [MSBuildTestMethod]
        public void TestMergeResultsBad1()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());

                result.MergeResults(null!);
            });
        }

        [MSBuildTestMethod]
        public void TestMergeResultsBad3()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());

                BuildRequest request2 = CreateNewBuildRequest(2, Array.Empty<string>());
                BuildResult result2 = new BuildResult(request2);
                result2.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());

                result.MergeResults(result2);
            });
        }
        [MSBuildTestMethod]
        public void TestHasResultsForTarget()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());

            Assert.IsTrue(result.HasResultsForTarget("foo"));
            Assert.IsFalse(result.HasResultsForTarget("bar"));
        }

        [MSBuildTestMethod]
        public void TestEnumerator()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            int countFound = result.ResultsByTarget?.Count ?? 0;
            Assert.AreEqual(0, countFound);

            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            bool foundFoo = false;
            countFound = 0;
            if (result.ResultsByTarget != null)
            {
                foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
                {
                    if (resultPair.Key == "foo")
                    {
                        foundFoo = true;
                    }

                    countFound++;
                }
            }

            Assert.AreEqual(1, countFound);
            Assert.IsTrue(foundFoo);

            result.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());
            foundFoo = false;
            bool foundBar = false;
            countFound = 0;
            if (result.ResultsByTarget != null)
            {
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
            }

            Assert.AreEqual(2, countFound);
            Assert.IsTrue(foundFoo);
            Assert.IsTrue(foundBar);
        }

        [MSBuildTestMethod]
        public void TestTranslation()
        {
            BuildRequest request = new BuildRequest(1, 1, 2, new string[] { "alpha", "omega" }, null, new BuildEventContext(1, 1, 2, 3, 4, 5), null);
            BuildResult result = new BuildResult(request, new BuildAbortedException());

            TaskItem fooTaskItem = new TaskItem("foo", "asdf.proj");
            fooTaskItem.SetMetadata("meta1", "metavalue1");
            fooTaskItem.SetMetadata("meta2", "metavalue2");

            result.InitialTargets = new List<string> { "a", "b" };
            result.DefaultTargets = new List<string> { "c", "d" };

            result.AddResultsForTarget("alpha", new TargetResult(new TaskItem[] { fooTaskItem }, BuildResultUtilities.GetSuccessResult()));
            result.AddResultsForTarget("omega", new TargetResult(Array.Empty<TaskItem>(), BuildResultUtilities.GetStopWithErrorResult(new ArgumentException("The argument was invalid"))));

            Assert.AreEqual(NodePacketType.BuildResult, (result as INodePacket).Type);
            ((ITranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildResult deserializedResult = (packet as BuildResult)!;

            Assert.AreEqual(result.ConfigurationId, deserializedResult.ConfigurationId);
            Assert.IsTrue(TranslationHelpers.CompareCollections(result.DefaultTargets, deserializedResult.DefaultTargets, StringComparer.Ordinal));
            Assert.IsTrue(TranslationHelpers.CompareExceptions(result.Exception, deserializedResult.Exception, out string diffReason), diffReason);
            Assert.AreEqual(result.Exception?.Message, deserializedResult.Exception?.Message);
            Assert.AreEqual(result.GlobalRequestId, deserializedResult.GlobalRequestId);
            Assert.IsTrue(TranslationHelpers.CompareCollections(result.InitialTargets, deserializedResult.InitialTargets, StringComparer.Ordinal));
            Assert.AreEqual(result.NodeRequestId, deserializedResult.NodeRequestId);
            Assert.AreEqual(result["alpha"].ResultCode, deserializedResult["alpha"].ResultCode);
            Assert.IsTrue(TranslationHelpers.CompareExceptions(result["alpha"].Exception, deserializedResult["alpha"].Exception, out diffReason), diffReason);
            Assert.IsTrue(TranslationHelpers.CompareCollections(result["alpha"].Items, deserializedResult["alpha"].Items, TaskItemComparer.Instance));
            Assert.AreEqual(result["omega"].ResultCode, deserializedResult["omega"].ResultCode);
            Assert.IsTrue(TranslationHelpers.CompareExceptions(result["omega"].Exception, deserializedResult["omega"].Exception, out diffReason), diffReason);
            Assert.IsTrue(TranslationHelpers.CompareCollections(result["omega"].Items, deserializedResult["omega"].Items, TaskItemComparer.Instance));
        }

        [MSBuildTestMethod]
        public void TestTranslationPreservesEvaluationId()
        {
            BuildRequest request = new(1, 1, 2, ["Build"], null, new BuildEventContext(1, 1, 2, 3, 4, 5), null);
            BuildResult result = new(request, new BuildAbortedException())
            {
                EvaluationId = 42,
            };

            ((ITranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());
            BuildResult deserializedResult = (packet as BuildResult)!;

            deserializedResult.EvaluationId.ShouldBe(42);
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}
