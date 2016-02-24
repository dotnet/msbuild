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
    public class GivenATestRunnerTestResultMessageHandler
    {
        private Mock<IDotnetTest> _dotnetTestMock;
        private Mock<IReportingChannel> _adapterChannelMock;

        private Message _validMessage;
        private TestRunnerTestResultMessageHandler _testRunnerTestResultMessageHandler;

        public GivenATestRunnerTestResultMessageHandler()
        {
            _dotnetTestMock = new Mock<IDotnetTest>();
            _dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.TestExecutionStarted);

            _adapterChannelMock = new Mock<IReportingChannel>();

            _validMessage = new Message
            {
                MessageType = TestMessageTypes.TestRunnerTestResult,
                Payload = JToken.FromObject("testFound")
            };

            _testRunnerTestResultMessageHandler = new TestRunnerTestResultMessageHandler(_adapterChannelMock.Object);
        }

        [Fact]
        public void It_returns_NoOp_if_the_dotnet_test_state_is_not_TestExecutionStarted()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.Terminated);

            var nextState = _testRunnerTestResultMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                _validMessage);

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_TestRunnerTestResult()
        {
            var nextState = _testRunnerTestResultMessageHandler.HandleMessage(
                _dotnetTestMock.Object,
                new Message { MessageType = "Something different from TestRunner.TestResult" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_TestExecutionStarted_when_it_handles_the_message()
        {
            var nextState = _testRunnerTestResultMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestExecutionStarted);
        }

        [Fact]
        public void It_sends_the_payload_of_the_message_when_it_handles_the_message()
        {
            _adapterChannelMock.Setup(a => a.Send(It.Is<Message>(m =>
                m.MessageType == TestMessageTypes.TestExecutionTestResult &&
                m.Payload.ToObject<string>() == _validMessage.Payload.ToObject<string>()))).Verifiable();

            _testRunnerTestResultMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _adapterChannelMock.Verify();
        }
    }
}
