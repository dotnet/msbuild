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
    public class GivenATestRunnerTestStartedMessageHandler
    {
        private Mock<IDotnetTest> _dotnetTestMock;
        private Mock<IReportingChannel> _adapterChannelMock;

        private Message _validMessage;
        private TestRunnerTestStartedMessageHandler _testRunnerTestStartedMessageHandler;

        public GivenATestRunnerTestStartedMessageHandler()
        {
            _dotnetTestMock = new Mock<IDotnetTest>();
            _dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo);

            _adapterChannelMock = new Mock<IReportingChannel>();

            _validMessage = new Message
            {
                MessageType = TestMessageTypes.TestRunnerTestStarted,
                Payload = JToken.FromObject("testFound")
            };

            _testRunnerTestStartedMessageHandler =
                new TestRunnerTestStartedMessageHandler(_adapterChannelMock.Object);
        }

        [Fact]
        public void It_returns_NoOp_if_the_dotnet_test_state_is_not_TestExecutionSentTestRunnerProcessStartInfo_or_TestExecutionTestStarted()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.Terminated);

            var nextState = _testRunnerTestStartedMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                _validMessage);

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_TestRunnerTestStarted()
        {
            var nextState = _testRunnerTestStartedMessageHandler.HandleMessage(
                _dotnetTestMock.Object,
                new Message { MessageType = "Something different from TestRunner.TestStart" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_TestExecutionStarted_when_it_handles_the_message_and_current_state_is_TestExecutionSentTestRunnerProcessStartInfo()
        {
            var nextState = _testRunnerTestStartedMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestExecutionStarted);
        }

        [Fact]
        public void It_returns_TestExecutionStarted_when_it_handles_the_message_and_current_state_is_TestExecutionTestStarted()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.TestExecutionStarted);

            var nextState = _testRunnerTestStartedMessageHandler.HandleMessage(
                    dotnetTestMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestExecutionStarted);
        }

        [Fact]
        public void It_sends_a_TestExecutionTestStarted_when_it_handles_the_message()
        {
            _adapterChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.TestExecutionStarted)))
                .Verifiable();

            _testRunnerTestStartedMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _adapterChannelMock.Verify();
        }

        [Fact]
        public void It_sends_the_payload_of_the_message_when_it_handles_the_message()
        {
            _adapterChannelMock.Setup(a => a.Send(It.Is<Message>(m =>
                m.MessageType == TestMessageTypes.TestExecutionStarted &&
                m.Payload.ToObject<string>() == _validMessage.Payload.ToObject<string>()))).Verifiable();

            _testRunnerTestStartedMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _adapterChannelMock.Verify();
        }
    }
}
