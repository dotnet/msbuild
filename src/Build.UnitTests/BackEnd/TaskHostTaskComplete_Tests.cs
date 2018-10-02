// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;


using TaskHostPacketHelpers = Microsoft.Build.UnitTests.BackEnd.TaskHostConfiguration_Tests.TaskHostPacketHelpers;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit Tests for TaskHostTaskComplete packet.
    /// </summary>
    public class TaskHostTaskComplete_Tests
    {
        /// <summary>
        /// Tests various valid ways to construct this packet.  
        /// </summary>
        [Fact]
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
        [Fact]
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
        [Fact]
        public void TestTranslationWithNullDictionary()
        {
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.Equal(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.NotNull(deserializedComplete.TaskOutputParameters);
            Assert.Equal(0, deserializedComplete.TaskOutputParameters.Count);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary is empty
        /// </summary>
        [Fact]
        public void TestTranslationWithEmptyDictionary()
        {
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, new Dictionary<string, object>()), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.Equal(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.NotNull(deserializedComplete.TaskOutputParameters);
            Assert.Equal(complete.TaskOutputParameters.Count, deserializedComplete.TaskOutputParameters.Count);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains only value types
        /// </summary>
        [Fact]
        public void TestTranslationWithValueTypesInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("Text", "Foo");
            parameters.Add("BoolValue", false);
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.Equal(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.NotNull(deserializedComplete.TaskOutputParameters);
            Assert.Equal(complete.TaskOutputParameters.Count, deserializedComplete.TaskOutputParameters.Count);
            Assert.Equal(complete.TaskOutputParameters["Text"].WrappedParameter, deserializedComplete.TaskOutputParameters["Text"].WrappedParameter);
            Assert.Equal(complete.TaskOutputParameters["BoolValue"].WrappedParameter, deserializedComplete.TaskOutputParameters["BoolValue"].WrappedParameter);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem. 
        /// </summary>
        [Fact]
        public void TestTranslationWithITaskItemInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskItemValue", new TaskItem("Foo"));
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.Equal(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.NotNull(deserializedComplete.TaskOutputParameters);
            Assert.Equal(complete.TaskOutputParameters.Count, deserializedComplete.TaskOutputParameters.Count);
            TaskHostPacketHelpers.AreEqual((ITaskItem)complete.TaskOutputParameters["TaskItemValue"].WrappedParameter, (ITaskItem)deserializedComplete.TaskOutputParameters["TaskItemValue"].WrappedParameter);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem array. 
        /// </summary>
        [Fact]
        public void TestTranslationWithITaskItemArrayInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskItemArrayValue", new ITaskItem[] { new TaskItem("Foo"), new TaskItem("Baz") });
            TaskHostTaskComplete complete = new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters), null);

            ((INodePacketTranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.Equal(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.NotNull(deserializedComplete.TaskOutputParameters);
            Assert.Equal(complete.TaskOutputParameters.Count, deserializedComplete.TaskOutputParameters.Count);

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
                Assert.IsAssignableFrom(expectedExceptionType, e); // "Wrong exception was thrown!"
            }

            Assert.True(exceptionCaught); // "No exception was caught when one was expected!"
        }
    }
}
