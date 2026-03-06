// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Pure unit tests for TaskHost callback packet serialization.
    /// No I/O or BuildManager — just round-trip translation.
    /// </summary>
    public class TaskHostCallbackPacket_Tests
    {
        [Fact]
        public void TaskHostIsRunningMultipleNodesRequest_RoundTrip_Serialization()
        {
            var request = new TaskHostIsRunningMultipleNodesRequest();
            request.RequestId = 42;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostIsRunningMultipleNodesRequest)TaskHostIsRunningMultipleNodesRequest.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(42);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostIsRunningMultipleNodesRequest);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TaskHostIsRunningMultipleNodesResponse_RoundTrip_Serialization(bool isRunningMultipleNodes)
        {
            var response = new TaskHostIsRunningMultipleNodesResponse(123, isRunningMultipleNodes);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostIsRunningMultipleNodesResponse)TaskHostIsRunningMultipleNodesResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(123);
            deserialized.IsRunningMultipleNodes.ShouldBe(isRunningMultipleNodes);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostIsRunningMultipleNodesResponse);
        }

        [Theory]
        [InlineData(4, false)]  // RequestCores(4)
        [InlineData(2, true)]   // ReleaseCores(2)
        public void TaskHostCoresRequest_RoundTrip_Serialization(int cores, bool isRelease)
        {
            var request = new TaskHostCoresRequest(cores, isRelease);
            request.RequestId = 77;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostCoresRequest)TaskHostCoresRequest.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(77);
            deserialized.RequestedCores.ShouldBe(cores);
            deserialized.IsRelease.ShouldBe(isRelease);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostCoresRequest);
        }

        [Theory]
        [InlineData(0)]   // ReleaseCores acknowledgment
        [InlineData(3)]   // RequestCores granted 3
        public void TaskHostCoresResponse_RoundTrip_Serialization(int grantedCores)
        {
            var response = new TaskHostCoresResponse(99, grantedCores);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostCoresResponse)TaskHostCoresResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(99);
            deserialized.GrantedCores.ShouldBe(grantedCores);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostCoresResponse);
        }
    }
}
