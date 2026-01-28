// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for TaskHostQueryRequest and TaskHostQueryResponse packets.
    /// </summary>
    public class TaskHostQueryPacket_Tests
    {
        [Fact]
        public void TaskHostQueryRequest_RoundTrip_Serialization()
        {
            var request = new TaskHostQueryRequest(TaskHostQueryRequest.QueryType.IsRunningMultipleNodes);
            request.RequestId = 42;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostQueryRequest)TaskHostQueryRequest.FactoryForDeserialization(readTranslator);

            deserialized.Query.ShouldBe(TaskHostQueryRequest.QueryType.IsRunningMultipleNodes);
            deserialized.RequestId.ShouldBe(42);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostQueryRequest);
        }

        [Fact]
        public void TaskHostQueryRequest_DefaultRequestId_IsZero()
        {
            var request = new TaskHostQueryRequest(TaskHostQueryRequest.QueryType.IsRunningMultipleNodes);
            request.RequestId.ShouldBe(0);
        }

        [Fact]
        public void TaskHostQueryResponse_RoundTrip_Serialization_True()
        {
            var response = new TaskHostQueryResponse(42, true);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostQueryResponse)TaskHostQueryResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(42);
            deserialized.BoolResult.ShouldBeTrue();
            deserialized.Type.ShouldBe(NodePacketType.TaskHostQueryResponse);
        }

        [Fact]
        public void TaskHostQueryResponse_RoundTrip_Serialization_False()
        {
            var response = new TaskHostQueryResponse(123, false);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostQueryResponse)TaskHostQueryResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(123);
            deserialized.BoolResult.ShouldBeFalse();
        }

        [Fact]
        public void TaskHostQueryRequest_ImplementsITaskHostCallbackPacket()
        {
            var request = new TaskHostQueryRequest(TaskHostQueryRequest.QueryType.IsRunningMultipleNodes);
            request.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
        }

        [Fact]
        public void TaskHostQueryResponse_ImplementsITaskHostCallbackPacket()
        {
            var response = new TaskHostQueryResponse(1, true);
            response.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
        }

        [Fact]
        public void TaskHostQueryRequest_RequestIdCanBeSet()
        {
            var request = new TaskHostQueryRequest(TaskHostQueryRequest.QueryType.IsRunningMultipleNodes);
            request.RequestId = 999;
            request.RequestId.ShouldBe(999);
        }

        [Fact]
        public void TaskHostQueryResponse_RequestIdCanBeSet()
        {
            var response = new TaskHostQueryResponse(1, true);
            response.RequestId = 888;
            response.RequestId.ShouldBe(888);
        }
    }
}
