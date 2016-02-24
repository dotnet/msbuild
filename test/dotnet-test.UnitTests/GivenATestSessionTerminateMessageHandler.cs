// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenATestSessionTerminateMessageHandler
    {
        private DotnetTestState _nextState;
        private Mock<ITestMessagesCollection> _testMessagesCollectionMock;

        public GivenATestSessionTerminateMessageHandler()
        {
            var reportingChannel = new Mock<IReportingChannel>();
            _testMessagesCollectionMock = new Mock<ITestMessagesCollection>();
            var dotnetTestMock = new Mock<IDotnetTest>();
            var messageHandler = new TestSessionTerminateMessageHandler(_testMessagesCollectionMock.Object);

            _nextState = messageHandler.HandleMessage(dotnetTestMock.Object, new Message
            {
                MessageType = TestMessageTypes.TestSessionTerminate
            });
        }

        [Fact]
        public void It_always_returns_the_terminated_state_idependent_of_the_state_passed_to_it()
        {
            _nextState.Should().Be(DotnetTestState.Terminated);
        }

        [Fact]
        public void It_calls_drain_on_the_test_messages()
        {
            _testMessagesCollectionMock.Verify(tmc => tmc.Drain(), Times.Once);
        }
    }
}
