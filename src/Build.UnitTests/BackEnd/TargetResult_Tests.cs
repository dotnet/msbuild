// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Engine.UnitTests.TestComparers;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Unittest;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the target result test.
    /// </summary>
    [TestClass]
    public class TargetResult_Tests
    {
        /// <summary>
        /// Tests a constructor with no items.
        /// </summary>
        [MSBuildTestMethod]
        public void TestConstructorNoItems()
        {
            TargetResult result = new TargetResult(Array.Empty<TaskItem>(), BuildResultUtilities.GetStopWithErrorResult());
            Assert.IsEmpty(result.Items);
            Assert.IsNull(result.Exception);
            Assert.AreEqual(TargetResultCode.Failure, result.ResultCode);
        }

        /// <summary>
        /// Tests a constructor with items.
        /// </summary>
        [MSBuildTestMethod]
        public void TestConstructorWithItems()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            TargetResult result = new TargetResult(new TaskItem[] { item }, BuildResultUtilities.GetStopWithErrorResult());
            Assert.ContainsSingle(result.Items);
            Assert.AreEqual(item.ItemSpec, result.Items[0].ItemSpec);
            Assert.AreEqual(TargetResultCode.Failure, result.ResultCode);
        }

        /// <summary>
        /// Tests a constructor with a null item array passed.
        /// </summary>
        [MSBuildTestMethod]
        public void TestConstructorNullItems()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                TargetResult result = new TargetResult(null, BuildResultUtilities.GetStopWithErrorResult());
            });
        }
        /// <summary>
        /// Tests a constructor with an exception passed.
        /// </summary>
        [MSBuildTestMethod]
        public void TestConstructorWithException()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            TargetResult result = new TargetResult(new TaskItem[] { item }, BuildResultUtilities.GetStopWithErrorResult(new ArgumentException()));
            Assert.ContainsSingle(result.Items);
            Assert.IsNotNull(result.Exception);
            Assert.AreEqual(typeof(ArgumentException), result.Exception.GetType());
            Assert.AreEqual(TargetResultCode.Failure, result.ResultCode);
        }

        /// <summary>
        /// Tests a constructor with a null exception passed.
        /// </summary>
        [MSBuildTestMethod]
        public void TestConstructorWithExceptionNull()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            TargetResult result = new TargetResult(new TaskItem[] { item }, BuildResultUtilities.GetStopWithErrorResult());
            Assert.ContainsSingle(result.Items);
            Assert.IsNull(result.Exception);
            Assert.AreEqual(TargetResultCode.Failure, result.ResultCode);
        }

        /// <summary>
        /// Tests serialization with no exception in the result.
        /// </summary>
        [MSBuildTestMethod]
        public void TestTranslationNoException()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            item.SetMetadata("a", "b");
            var buildEventContext = new Framework.BuildEventContext(1, 2, 3, 4, 5, 6, 7);

            TargetResult result = new TargetResult(
                new TaskItem[] { item },
                BuildResultUtilities.GetStopWithErrorResult(),
                buildEventContext);

            ((ITranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            TargetResult deserializedResult = TargetResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(result.ResultCode, deserializedResult.ResultCode);
            Assert.IsTrue(TranslationHelpers.CompareCollections(result.Items, deserializedResult.Items, TaskItemComparer.Instance));
            Assert.IsTrue(TranslationHelpers.CompareExceptions(result.Exception, deserializedResult.Exception, out string diffReason), diffReason);
            Assert.AreEqual(result.OriginalBuildEventContext, deserializedResult.OriginalBuildEventContext);
        }

        /// <summary>
        /// Tests serialization with an exception in the result.
        /// </summary>
        [MSBuildTestMethod]
        public void TestTranslationWithException()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            item.SetMetadata("a", "b");

            TargetResult result = new TargetResult(new TaskItem[] { item }, BuildResultUtilities.GetStopWithErrorResult(new BuildAbortedException()));

            ((ITranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            TargetResult deserializedResult = TargetResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(result.ResultCode, deserializedResult.ResultCode);
            Assert.IsTrue(TranslationHelpers.CompareCollections(result.Items, deserializedResult.Items, TaskItemComparer.Instance));
            Assert.IsTrue(TranslationHelpers.CompareExceptions(result.Exception, deserializedResult.Exception, out string diffReason), diffReason);
        }

        /// <summary>
        /// Test GetCacheDirectory is resilient to paths with strings that would normally make string.format to throw a FormatException
        /// </summary>
        [MSBuildTestMethod]
        public void TestGetCacheDirectory()
        {
            string oldTmp = Environment.GetEnvironmentVariable("TMP");

            try
            {
                Environment.SetEnvironmentVariable("TMP", "C:\\}");
                string path1 = TargetResult.GetCacheDirectory(2, "Blah");

                Environment.SetEnvironmentVariable("TMP", "C:\\{");
                string path2 = TargetResult.GetCacheDirectory(2, "Blah");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTmp);
            }
        }
    }
}
