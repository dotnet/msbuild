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
    public class GivenATestRunnerTestCompletedMessageHandler
    {
        private Mock<IDotnetTest> _dotnetTestAtTestDiscoveryStartedMock;
        private Mock<IDotnetTest> _dotnetTestAtTestExecutionStartedMock;
        private Mock<IReportingChannel> _adapterChannelMock;

        private Message _validMessage;
        private TestRunnerTestCompletedMessageHandler _testRunnerTestCompletedMessageHandler;

        public GivenATestRunnerTestCompletedMessageHandler()
        {
            _dotnetTestAtTestDiscoveryStartedMock = new Mock<IDotnetTest>();
            _dotnetTestAtTestDiscoveryStartedMock.Setup(d => d.State).Returns(DotnetTestState.TestDiscoveryStarted);

            _dotnetTestAtTestExecutionStartedMock = new Mock<IDotnetTest>();
            _dotnetTestAtTestExecutionStartedMock.Setup(d => d.State).Returns(DotnetTestState.TestExecutionStarted);

            _adapterChannelMock = new Mock<IReportingChannel>();

            _validMessage = new Message
            {
                MessageType = TestMessageTypes.TestRunnerTestCompleted
            };

            _testRunnerTestCompletedMessageHandler =
                new TestRunnerTestCompletedMessageHandler(_adapterChannelMock.Object);
        }

        [Fact]
        public void It_returns_NoOp_if_the_dotnet_test_state_is_not_TestDiscoveryStarted_or_TestExecutionStarted()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.Terminated);

            var nextState = _testRunnerTestCompletedMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                _validMessage);

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_TestRunnerTestCompleted_when_state_is_TestDiscoveryStarted()
        {
            var nextState = _testRunnerTestCompletedMessageHandler.HandleMessage(
                _dotnetTestAtTestDiscoveryStartedMock.Object,
                new Message { MessageType = "Something different from TestDiscovery.Start" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_TestRunnerTestCompleted_when_state_is_TestExecutionStarted()
        {
            var nextState = _testRunnerTestCompletedMessageHandler.HandleMessage(
                _dotnetTestAtTestExecutionStartedMock.Object,
                new Message { MessageType = "Something different from TestDiscovery.Start" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_TestDiscoveryCompleted_when_it_handles_the_message_and_current_state_is_TestDiscoveryStarted()
        {
            var nextState = _testRunnerTestCompletedMessageHandler.HandleMessage(
                    _dotnetTestAtTestDiscoveryStartedMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestDiscoveryCompleted);
        }

        [Fact]
        public void It_sends_a_TestDiscoveryCompleted_when_it_handles_the_message_and_current_state_is_TestDiscoveryStarted()
        {
            _adapterChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.TestDiscoveryCompleted)))
                .Verifiable();

            _testRunnerTestCompletedMessageHandler.HandleMessage(
                    _dotnetTestAtTestDiscoveryStartedMock.Object,
                    _validMessage);

            _adapterChannelMock.Verify();
        }

        [Fact]
        public void It_returns_TestExecutionCompleted_when_it_handles_the_message_and_current_state_is_TestExecutionStarted()
        {
            var nextState = _testRunnerTestCompletedMessageHandler.HandleMessage(
                    _dotnetTestAtTestExecutionStartedMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestExecutionCompleted);
        }

        [Fact]
        public void It_sends_a_TestExecutionCompleted_when_it_handles_the_message_and_current_state_is_TestExecutionStarted()
        {
            _adapterChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.TestExecutionCompleted)))
                .Verifiable();

            _testRunnerTestCompletedMessageHandler.HandleMessage(
                    _dotnetTestAtTestExecutionStartedMock.Object,
                    _validMessage);

            _adapterChannelMock.Verify();
        }
    }
}
