// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Xunit;
using System.Linq;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenATestRunnerWaitingCommandMessageHandler
    {
        private Mock<IDotnetTest> _dotnetTestMock;
        private Mock<IReportingChannel> _testRunnerChannelMock;
        private Mock<IReportingChannelFactory> _reportingChannelFactory;
        private List<string> _testsToRun;

        private Message _validMessage;
        private TestRunnerWaitingCommandMessageHandler _testRunnerWaitingCommandMessageHandler;

        public GivenATestRunnerWaitingCommandMessageHandler()
        {
            _testsToRun = new List<string> { "test1", "test2" };
            _dotnetTestMock = new Mock<IDotnetTest>();
            _dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo);
            _dotnetTestMock.Setup(d => d.TestsToRun).Returns(_testsToRun);

            _validMessage = new Message
            {
                MessageType = TestMessageTypes.TestRunnerWaitingCommand
            };

            _testRunnerChannelMock = new Mock<IReportingChannel>();
            _reportingChannelFactory = new Mock<IReportingChannelFactory>();

            _testRunnerWaitingCommandMessageHandler =
                new TestRunnerWaitingCommandMessageHandler(_reportingChannelFactory.Object);
        }

        [Fact]
        public void It_returns_NoOp_if_the_dotnet_test_state_is_not_TestExecutionSentTestRunnerProcessStartInfo_or_TestExecutionTestStarted()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.Terminated);

            var nextState = _testRunnerWaitingCommandMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                _validMessage);

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_TestRunnerWaitingCommand()
        {
            var nextState = _testRunnerWaitingCommandMessageHandler.HandleMessage(
                _dotnetTestMock.Object,
                new Message { MessageType = "Something different from TestRunner.WaitingCommand" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_TestExecutionSentTestRunnerProcessStartInfo_when_it_handles_the_message()
        {
            _reportingChannelFactory.Raise(
                r => r.TestRunnerChannelCreated += null,
                _reportingChannelFactory.Object, _testRunnerChannelMock.Object);

            var nextState = _testRunnerWaitingCommandMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo);
        }

        [Fact]
        public void It_sends_a_TestRunnerExecute_when_it_handles_the_message()
        {
            _reportingChannelFactory.Raise(
                r => r.TestRunnerChannelCreated += null,
                _reportingChannelFactory.Object, _testRunnerChannelMock.Object);

            _testRunnerChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.TestRunnerExecute)))
                .Verifiable();

            _testRunnerWaitingCommandMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _testRunnerChannelMock.Verify();
        }

        [Fact]
        public void It_sends_a_the_list_of_tests_to_run_when_it_handles_the_message()
        {
            _testRunnerChannelMock.Setup(a => a.Send(It.Is<Message>(m =>
                m.MessageType == TestMessageTypes.TestRunnerExecute &&
                m.Payload.ToObject<RunTestsMessage>().Tests.All(t => _testsToRun.Contains(t)) &&
                m.Payload.ToObject<RunTestsMessage>().Tests.Count == _testsToRun.Count))).Verifiable();

            _reportingChannelFactory.Raise(
                r => r.TestRunnerChannelCreated += null,
                _reportingChannelFactory.Object, _testRunnerChannelMock.Object);

            _testRunnerWaitingCommandMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _testRunnerChannelMock.Verify();
        }

        [Fact]
        public void It_throws_InvalidOperationException_when_a_second_test_runner_channel_gets_created()
        {
            _reportingChannelFactory.Raise(
                r => r.TestRunnerChannelCreated += null,
                _reportingChannelFactory.Object, _testRunnerChannelMock.Object);

            Action action = () => _reportingChannelFactory.Raise(
                r => r.TestRunnerChannelCreated += null,
                _reportingChannelFactory.Object, _testRunnerChannelMock.Object);

            const string errorMessage = "TestRunnerWaitingCommandMessageHandler already has a test runner channel";
            action.ShouldThrow<InvalidOperationException>().WithMessage(errorMessage);
        }

        [Fact]
        public void It_throws_InvalidOperationException_when_no_test_runner_channel_has_been_created()
        {
            Action action = () => _testRunnerWaitingCommandMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            const string errorMessage =
                    "A test runner channel hasn't been created for TestRunnerWaitingCommandMessageHandler";
            action.ShouldThrow<InvalidOperationException>().WithMessage(errorMessage);
        }
    }
}
