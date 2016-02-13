// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenATestRunnerTestFoundMessageHandler
    {
        private Mock<IDotnetTest> _dotnetTestMock;
        private Mock<IReportingChannel> _adapterChannelMock;

        private Message _validMessage;
        private TestRunnerTestFoundMessageHandler _testRunnerTestFoundMessageHandler;

        public GivenATestRunnerTestFoundMessageHandler()
        {
            _dotnetTestMock = new Mock<IDotnetTest>();
            _dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.TestDiscoveryStarted);

            _adapterChannelMock = new Mock<IReportingChannel>();

            _validMessage = new Message
            {
                MessageType = TestMessageTypes.TestRunnerTestFound,
                Payload = JToken.FromObject("testFound")
            };

            _testRunnerTestFoundMessageHandler = new TestRunnerTestFoundMessageHandler(_adapterChannelMock.Object);
        }

        [Fact]
        public void It_returns_NoOp_if_the_dotnet_test_state_is_not_TestDiscoveryStarted()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.Terminated);

            var nextState = _testRunnerTestFoundMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                _validMessage);

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_TestRunnerTestFound()
        {
            var nextState = _testRunnerTestFoundMessageHandler.HandleMessage(
                _dotnetTestMock.Object,
                new Message { MessageType = "Something different from TestDiscovery.Start" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_TestDiscoveryStarted_when_it_handles_the_message()
        {
            var nextState = _testRunnerTestFoundMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestDiscoveryStarted);
        }

        [Fact]
        public void It_sends_the_payload_of_the_message_when_it_handles_the_message()
        {
            _adapterChannelMock.Setup(a => a.Send(It.Is<Message>(m =>
                m.MessageType == TestMessageTypes.TestDiscoveryTestFound &&
                m.Payload.ToObject<string>() == _validMessage.Payload.ToObject<string>()))).Verifiable();

            _testRunnerTestFoundMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _adapterChannelMock.Verify();
        }
    }
}
