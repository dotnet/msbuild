// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.FileAccess;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using TaskHostPacketHelpers = Microsoft.Build.UnitTests.BackEnd.TaskHostConfiguration_Tests.TaskHostPacketHelpers;

#nullable disable

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
#if FEATURE_REPORTFILEACCESSES
            var fileAccessData = new List<FileAccessData>()
            {
                new FileAccessData(
                    ReportedFileOperation.CreateFile,
                    RequestedAccess.Read,
                    processId: 123,
                    id: 1,
                    correlationId: 0,
                    error: 0,
                    DesiredAccess.GENERIC_READ,
                    FlagsAndAttributes.FILE_ATTRIBUTE_NORMAL,
                    "foo",
                    null,
                    true),
            };
#endif

            _ = new TaskHostTaskComplete(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success),
#if FEATURE_REPORTFILEACCESSES
                fileAccessData,
#endif
                null);
            _ = new TaskHostTaskComplete(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Failure),
#if FEATURE_REPORTFILEACCESSES
                fileAccessData,
#endif
                null);
            _ = new TaskHostTaskComplete(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedDuringInitialization,
                new ArgumentOutOfRangeException()),
#if FEATURE_REPORTFILEACCESSES
                fileAccessData,
#endif
                null);
            _ = new TaskHostTaskComplete(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedDuringExecution, new ArgumentNullException()),
#if FEATURE_REPORTFILEACCESSES
                fileAccessData,
#endif
                null);

            IDictionary<string, object> parameters = new Dictionary<string, object>();
            _ = new TaskHostTaskComplete(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null);

            IDictionary<string, object> parameters2 = new Dictionary<string, object>();
            parameters2.Add("Text", "Hello!");
            parameters2.Add("MyBoolValue", true);
            parameters2.Add("MyITaskItem", new TaskItem("ABC"));
            parameters2.Add("ItemArray", new ITaskItem[] { new TaskItem("DEF"), new TaskItem("GHI"), new TaskItem("JKL") });
            _ = new TaskHostTaskComplete(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters2),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null);
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
            TaskHostTaskComplete complete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null);

            ((ITranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskComplete.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostTaskComplete deserializedComplete = packet as TaskHostTaskComplete;

            Assert.Equal(complete.TaskResult, deserializedComplete.TaskResult);
            Assert.NotNull(deserializedComplete.TaskOutputParameters);
            Assert.Empty(deserializedComplete.TaskOutputParameters);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary is empty
        /// </summary>
        [Fact]
        public void TestTranslationWithEmptyDictionary()
        {
            TaskHostTaskComplete complete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, new Dictionary<string, object>()),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null);

            ((ITranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
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
            TaskHostTaskComplete complete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null);

            ((ITranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
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
            TaskHostTaskComplete complete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null);

            ((ITranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
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
            TaskHostTaskComplete complete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success, parameters),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null);

            ((ITranslatable)complete).Translate(TranslationHelpers.GetWriteTranslator());
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
        /// With the environment-delta wire format (packet version >= 5) and the default
        /// <see cref="InvariantPayloadTransferMode.Full"/> mode, the build process environment
        /// is serialized in full and round-trips correctly.
        /// </summary>
        [Fact]
        public void TestTranslationEnvironmentFullRoundTripsAtVersion5()
        {
            Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = @"c:\windows;c:\windows\system32",
                ["TEMP"] = @"c:\temp",
            };

            TaskHostTaskComplete complete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                environment);
            complete.EnvironmentMode.ShouldBe(InvariantPayloadTransferMode.Full);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 5;
            ((ITranslatable)complete).Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 5;
            TaskHostTaskComplete deserializedComplete = (TaskHostTaskComplete)TaskHostTaskComplete.FactoryForDeserialization(readTranslator);

            deserializedComplete.EnvironmentMode.ShouldBe(InvariantPayloadTransferMode.Full);
            deserializedComplete.BuildProcessEnvironment.ShouldNotBeNull();
            deserializedComplete.BuildProcessEnvironment.Count.ShouldBe(environment.Count);
            deserializedComplete.BuildProcessEnvironment["PATH"].ShouldBe(environment["PATH"]);
            deserializedComplete.BuildProcessEnvironment["TEMP"].ShouldBe(environment["TEMP"]);
        }

        /// <summary>
        /// With the environment-delta wire format (packet version >= 5) and
        /// <see cref="InvariantPayloadTransferMode.Identical"/> mode, the build process environment
        /// dictionary is omitted from the wire (saving the ~6 KB echo) and is null after deserialization;
        /// the parent reconstructs it from the configuration it sent for the task.
        /// </summary>
        [Fact]
        public void TestTranslationEnvironmentIdenticalOmitsDictionaryAtVersion5()
        {
            Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = @"c:\windows;c:\windows\system32",
                ["TEMP"] = @"c:\temp",
            };

            TaskHostTaskComplete fullComplete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                environment);
            ITranslator fullWriter = TranslationHelpers.GetWriteTranslator();
            fullWriter.NegotiatedPacketVersion = 5;
            ((ITranslatable)fullComplete).Translate(fullWriter);
            long fullLength = TranslationHelpers.GetWriteStreamLength();

            TaskHostTaskComplete complete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                environment);
            complete.EnvironmentMode = InvariantPayloadTransferMode.Identical;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 5;
            ((ITranslatable)complete).Translate(writeTranslator);
            TranslationHelpers.GetWriteStreamLength().ShouldBeLessThan(fullLength);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 5;
            TaskHostTaskComplete deserializedComplete = (TaskHostTaskComplete)TaskHostTaskComplete.FactoryForDeserialization(readTranslator);

            deserializedComplete.EnvironmentMode.ShouldBe(InvariantPayloadTransferMode.Identical);
            deserializedComplete.BuildProcessEnvironment.ShouldBeNull();
        }

        /// <summary>
        /// For a negotiated packet version below 5 (a legacy peer) the return-path delta wire format is not used:
        /// the build process environment is serialized in the legacy full-dictionary format. Guards the Math.Min
        /// version-negotiation fallback so older hosts keep working.
        /// </summary>
        [Fact]
        public void TestTranslationLegacyVersionSerializesFullEnvironment()
        {
            Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = @"c:\windows;c:\windows\system32",
                ["TEMP"] = @"c:\temp",
            };

            TaskHostTaskComplete complete = new(
                new OutOfProcTaskHostTaskResult(TaskCompleteType.Success),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                environment);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 4;
            ((ITranslatable)complete).Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 4;
            TaskHostTaskComplete deserializedComplete = (TaskHostTaskComplete)TaskHostTaskComplete.FactoryForDeserialization(readTranslator);

            // Legacy format always carries the full environment dictionary on the wire.
            deserializedComplete.BuildProcessEnvironment.ShouldNotBeNull();
            deserializedComplete.BuildProcessEnvironment.Count.ShouldBe(environment.Count);
            deserializedComplete.BuildProcessEnvironment["PATH"].ShouldBe(environment["PATH"]);
            deserializedComplete.BuildProcessEnvironment["TEMP"].ShouldBe(environment["TEMP"]);
        }

        /// <summary>
        /// Helper method for testing invalid constructors
        /// </summary>
        private void AssertInvalidConstructorThrows(Type expectedExceptionType, TaskCompleteType taskResult, Exception taskException, string taskExceptionMessage, string[] taskExceptionMessageArgs, IDictionary<string, object> taskOutputParameters, IDictionary<string, string> buildProcessEnvironment)
        {
            bool exceptionCaught = false;

            try
            {
                TaskHostTaskComplete complete = new(
                    new OutOfProcTaskHostTaskResult(taskResult, taskOutputParameters, taskException, taskExceptionMessage, taskExceptionMessageArgs),
#if FEATURE_REPORTFILEACCESSES
                    null,
#endif
                    buildProcessEnvironment);
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
