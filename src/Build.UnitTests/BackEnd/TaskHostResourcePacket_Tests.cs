// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for TaskHostResourceRequest and TaskHostResourceResponse packets.
    /// </summary>
    public class TaskHostResourcePacket_Tests
    {
        [Fact]
        public void TaskHostResourceRequest_RequestCores_RoundTrip()
        {
            var request = new TaskHostResourceRequest(
                TaskHostResourceRequest.ResourceOperation.RequestCores, 4);
            request.RequestId = 42;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostResourceRequest)TaskHostResourceRequest.FactoryForDeserialization(readTranslator);

            deserialized.Operation.ShouldBe(TaskHostResourceRequest.ResourceOperation.RequestCores);
            deserialized.CoreCount.ShouldBe(4);
            deserialized.RequestId.ShouldBe(42);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostResourceRequest);
        }

        [Fact]
        public void TaskHostResourceRequest_ReleaseCores_RoundTrip()
        {
            var request = new TaskHostResourceRequest(
                TaskHostResourceRequest.ResourceOperation.ReleaseCores, 2);
            request.RequestId = 43;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostResourceRequest)TaskHostResourceRequest.FactoryForDeserialization(readTranslator);

            deserialized.Operation.ShouldBe(TaskHostResourceRequest.ResourceOperation.ReleaseCores);
            deserialized.CoreCount.ShouldBe(2);
            deserialized.RequestId.ShouldBe(43);
        }

        [Fact]
        public void TaskHostResourceResponse_RoundTrip()
        {
            var response = new TaskHostResourceResponse(42, 3);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostResourceResponse)TaskHostResourceResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(42);
            deserialized.CoresGranted.ShouldBe(3);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostResourceResponse);
        }

        [Fact]
        public void TaskHostResourceRequest_DefaultConstructor_HasCorrectType()
        {
            var request = new TaskHostResourceRequest();
            request.Type.ShouldBe(NodePacketType.TaskHostResourceRequest);
        }

        [Fact]
        public void TaskHostResourceResponse_DefaultConstructor_HasCorrectType()
        {
            var response = new TaskHostResourceResponse();
            response.Type.ShouldBe(NodePacketType.TaskHostResourceResponse);
        }

        [Fact]
        public void TaskHostResourceRequest_ImplementsITaskHostCallbackPacket()
        {
            var request = new TaskHostResourceRequest(
                TaskHostResourceRequest.ResourceOperation.RequestCores, 1);
            request.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
        }

        [Fact]
        public void TaskHostResourceResponse_ImplementsITaskHostCallbackPacket()
        {
            var response = new TaskHostResourceResponse(1, 2);
            response.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
        }

        [Fact]
        public void TaskHostResourceRequest_RequestIdProperty_CanBeSet()
        {
            var request = new TaskHostResourceRequest(
                TaskHostResourceRequest.ResourceOperation.RequestCores, 1);
            request.RequestId = 999;
            request.RequestId.ShouldBe(999);
        }

        [Fact]
        public void TaskHostResourceResponse_RequestIdProperty_CanBeSet()
        {
            var response = new TaskHostResourceResponse(1, 2);
            response.RequestId = 888;
            response.RequestId.ShouldBe(888);
        }
    }
}
