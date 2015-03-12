// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit Tests for TaskHostTaskComplete packet.</summary>
//-----------------------------------------------------------------------

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using TaskHostPacketHelpers = Microsoft.Build.UnitTests.BackEnd.TaskHostConfiguration_Tests.TaskHostPacketHelpers;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit Tests for TaskHostTaskComplete packet.
    /// </summary>
    [TestClass]
    public class TaskHostTaskComplete_Tests
    {
        /// <summary>
        /// Tests various valid ways to construct this packet.  
        /// </summary>
        [TestMethod]
        public void TestConstructors()
        {
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success), null);
            TaskHostTaskComplete complete2 = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Failure), null);
            TaskHostTaskComplete complete3 = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedDuringInitialization, new ArgumentOutOfRangeException()), null);
            TaskHostTaskComplete complete4 = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedDuringExecution, new ArgumentNullException()), null);

            IDictionary<string, object> parameters = new Dictionary<string, object>();
            TaskHostTaskComplete complete5 = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters), null);

            IDictionary<string, object> parameters2 = new Dictionary<string, object>();
            parameters2.Add("Text", "Hello!");
            parameters2.Add("MyBoolValue", true);
            parameters2.Add("MyITaskItem", new TaskItem("ABC"));
            parameters2.Add("ItemArray", new ITaskItem[] { new TaskItem("DEF"), new TaskItem("GHI"), new TaskItem("JKL") });

            TaskHostTaskComplete complete6 = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters2), null);
        }

        /// <summary>
        /// Test invalid constructor permutations. 
        /// </summary>
        [TestMethod]
        public void TestInvalidConstructors()
        {
            AssertInvalidConstructorThrows(typeof(InternalErrorException), TaskCompleteType.CrashedDuringExecution, null, "ExceptionlessErrorMessage", null, null, null);
            AssertInvalidConstructorThrows(typeof(InternalErrorException), TaskCompleteType.CrashedDuringInitialization, null, null, null, null, null);
            AssertInvalidConstructorThrows(typeof(InternalErrorException), TaskCompleteType.Success, new ArgumentNullException(), "ExceptionlessErrorMessage", null, null, null);
            AssertInvalidConstructorThrows(typeof(InternalErrorException), TaskCompleteType.CrashedDuringExecution, null, null, new string[1] { "Foo" }, null, null);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary is null
        /// </summary>
        [TestMethod]
        public void TestTranslationWithNullDictionary()
        {
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.AreEqual(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.IsNotNull(deserializedComplete.TaskOutputParameters);
            Assert.AreEqual(0, deserializedComplete.TaskOutputParameters.Count);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary is empty
        /// </summary>
        [TestMethod]
        public void TestTranslationWithEmptyDictionary()
        {
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, new Dictionary<string, object>()), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.AreEqual(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.IsNotNull(deserializedComplete.TaskOutputParameters);
            Assert.AreEqual(complete.TaskOutputParameters.Count, deserializedComplete.TaskOutputParameters.Count);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains only value types
        /// </summary>
        [TestMethod]
        public void TestTranslationWithValueTypesInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("Text", "Foo");
            parameters.Add("BoolValue", false);
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.AreEqual(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.IsNotNull(deserializedComplete.TaskOutputParameters);
            Assert.AreEqual(complete.TaskOutputParameters.Count, deserializedComplete.TaskOutputParameters.Count);
            Assert.AreEqual(complete.TaskOutputParameters["Text"].WrappedParameter, deserializedComplete.TaskOutputParameters["Text"].WrappedParameter);
            Assert.AreEqual(complete.TaskOutputParameters["BoolValue"].WrappedParameter, deserializedComplete.TaskOutputParameters["BoolValue"].WrappedParameter);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem. 
        /// </summary>
        [TestMethod]
        public void TestTranslationWithITaskItemInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskItemValue", new TaskItem("Foo"));
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.AreEqual(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.IsNotNull(deserializedComplete.TaskOutputParameters);
            Assert.AreEqual(complete.TaskOutputParameters.Count, deserializedComplete.TaskOutputParameters.Count);
            TaskHostPacketHelpers.AreEqual((ITaskItem)complete.TaskOutputParameters["TaskItemValue"].WrappedParameter, (ITaskItem)deserializedComplete.TaskOutputParameters["TaskItemValue"].WrappedParameter);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem array. 
        /// </summary>
        [TestMethod]
        public void TestTranslationWithITaskItemArrayInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskItemArrayValue", new ITaskItem[] { new TaskItem("Foo"), new TaskItem("Baz") });
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.AreEqual(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.IsNotNull(deserializedComplete.TaskOutputParameters);
            Assert.AreEqual(complete.TaskOutputParameters.Count, deserializedComplete.TaskOutputParameters.Count);

            ITaskItem[] itemArray = (ITaskItem[])complete.TaskOutputParameters["TaskItemArrayValue"].WrappedParameter;
            ITaskItem[] deserializedItemArray = (ITaskItem[])deserializedComplete.TaskOutputParameters["TaskItemArrayValue"].WrappedParameter;

            TaskHostPacketHelpers.AreEqual(itemArray, deserializedItemArray);
        }

        /// <summary>
        /// Helper method for testing invalid constructors
        /// </summary>
        private void AssertInvalidConstructorThrows(Type expectedExceptionType, TaskCompleteType taskResult, Exception taskException, string taskExceptionMessage, string[] taskExceptionMessageArgs, IDictionary<string, object> taskOutputParameters, IDictionary<string, string> buildProcessEnvironment)
        {
            bool exceptionCaught = false;

            try
            {
                TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(taskResult, taskOutputParameters, taskException, taskExceptionMessage, taskExceptionMessageArgs), buildProcessEnvironment);
            }
            catch (Exception e)
            {
                exceptionCaught = true;
                Assert.IsInstanceOfType(e, expectedExceptionType, "Wrong exception was thrown!");
            }

            Assert.IsTrue(exceptionCaught, "No exception was caught when one was expected!");
        }
    }
}
