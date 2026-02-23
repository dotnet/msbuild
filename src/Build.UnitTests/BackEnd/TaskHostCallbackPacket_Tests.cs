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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TaskHostQueryResponse_RoundTrip_Serialization(bool boolResult)
        {
            var response = new TaskHostQueryResponse(123, boolResult);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostQueryResponse)TaskHostQueryResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(123);
            deserialized.BoolResult.ShouldBe(boolResult);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostQueryResponse);
        }
    }
}
