// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for TaskHostYieldRequest and TaskHostYieldResponse packets.
    /// </summary>
    public class TaskHostYieldPacket_Tests
    {
        [Fact]
        public void TaskHostYieldRequest_Yield_RoundTrip_Serialization()
        {
            var request = new TaskHostYieldRequest(42, YieldOperation.Yield);
            request.RequestId = 100;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostYieldRequest)TaskHostYieldRequest.FactoryForDeserialization(readTranslator);

            deserialized.TaskId.ShouldBe(42);
            deserialized.Operation.ShouldBe(YieldOperation.Yield);
            deserialized.RequestId.ShouldBe(100);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostYieldRequest);
        }

        [Fact]
        public void TaskHostYieldRequest_Reacquire_RoundTrip_Serialization()
        {
            var request = new TaskHostYieldRequest(99, YieldOperation.Reacquire);
            request.RequestId = 200;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostYieldRequest)TaskHostYieldRequest.FactoryForDeserialization(readTranslator);

            deserialized.TaskId.ShouldBe(99);
            deserialized.Operation.ShouldBe(YieldOperation.Reacquire);
            deserialized.RequestId.ShouldBe(200);
        }

        [Fact]
        public void TaskHostYieldRequest_DefaultRequestId_IsZero()
        {
            var request = new TaskHostYieldRequest(1, YieldOperation.Yield);
            request.RequestId.ShouldBe(0);
        }

        [Fact]
        public void TaskHostYieldRequest_ImplementsITaskHostCallbackPacket()
        {
            var request = new TaskHostYieldRequest(1, YieldOperation.Yield);
            request.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
        }

        [Fact]
        public void TaskHostYieldResponse_RoundTrip_Serialization_Success()
        {
            var response = new TaskHostYieldResponse(42, success: true);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostYieldResponse)TaskHostYieldResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(42);
            deserialized.Success.ShouldBeTrue();
            deserialized.Type.ShouldBe(NodePacketType.TaskHostYieldResponse);
        }

        [Fact]
        public void TaskHostYieldResponse_RoundTrip_Serialization_Failure()
        {
            var response = new TaskHostYieldResponse(123, success: false);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostYieldResponse)TaskHostYieldResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(123);
            deserialized.Success.ShouldBeFalse();
        }

        [Fact]
        public void TaskHostYieldResponse_ImplementsITaskHostCallbackPacket()
        {
            var response = new TaskHostYieldResponse(1, true);
            response.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
        }
    }
}
