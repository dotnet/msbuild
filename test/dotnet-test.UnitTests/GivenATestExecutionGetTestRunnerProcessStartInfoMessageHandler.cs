// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenATestExecutionGetTestRunnerProcessStartInfoMessageHandler
    {
        private const int TestRunnerPort = 1;
        private const string AssemblyUnderTest = "assembly.dll";

        private GetTestRunnerProcessStartInfoMessageHandler _testGetTestRunnerProcessStartInfoMessageHandler;
        private Message _validMessage;
        private TestStartInfo _testStartInfo;
        private List<string> _testsToRun;

        private Mock<ITestRunner> _testRunnerMock;
        private Mock<ITestRunnerFactory> _testRunnerFactoryMock;
        private Mock<IReportingChannel> _adapterChannelMock;
        private Mock<IReportingChannel> _testRunnerChannelMock;
        private Mock<IReportingChannelFactory> _reportingChannelFactoryMock;
        private Mock<IDotnetTest> _dotnetTestMock;

        private RunTestsArgumentsBuilder _argumentsBuilder;

        public GivenATestExecutionGetTestRunnerProcessStartInfoMessageHandler()
        {
            _testsToRun = new List<string> {"test1", "test2"};
            _validMessage = new Message
            {
                MessageType = TestMessageTypes.TestExecutionGetTestRunnerProcessStartInfo,
                Payload = JToken.FromObject(new RunTestsMessage { Tests = _testsToRun })
            };

            _dotnetTestMock = new Mock<IDotnetTest>();
            _dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.VersionCheckCompleted);
            _dotnetTestMock.Setup(d => d.PathToAssemblyUnderTest).Returns(AssemblyUnderTest);

            _testStartInfo = new TestStartInfo
            {
                FileName = "runner",
                Arguments = "arguments"
            };

            _testRunnerMock = new Mock<ITestRunner>();
            _testRunnerMock.Setup(t => t.GetProcessStartInfo()).Returns(_testStartInfo);

            _testRunnerFactoryMock = new Mock<ITestRunnerFactory>();
            _testRunnerFactoryMock
                .Setup(c => c.CreateTestRunner(It.IsAny<RunTestsArgumentsBuilder>()))
                .Callback<ITestRunnerArgumentsBuilder>(r => _argumentsBuilder = r as RunTestsArgumentsBuilder)
                .Returns(_testRunnerMock.Object);

            _adapterChannelMock = new Mock<IReportingChannel>();
            _testRunnerChannelMock = new Mock<IReportingChannel>();
            _testRunnerChannelMock.Setup(t => t.Port).Returns(TestRunnerPort);

            _reportingChannelFactoryMock = new Mock<IReportingChannelFactory>();
            _reportingChannelFactoryMock.Setup(r =>
                r.CreateTestRunnerChannel()).Returns(_testRunnerChannelMock.Object);

            _testGetTestRunnerProcessStartInfoMessageHandler = new GetTestRunnerProcessStartInfoMessageHandler(
                _testRunnerFactoryMock.Object,
                _adapterChannelMock.Object,
                _reportingChannelFactoryMock.Object);
        }

        [Fact]
        public void It_returns_NoOp_if_the_dotnet_test_state_is_not_VersionCheckCompleted_or_InitialState()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.Terminated);

            var nextState = _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                _validMessage);

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_TestDiscoveryStart()
        {
            var nextState = _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                _dotnetTestMock.Object,
                new Message { MessageType = "Something different from TestDiscovery.Start" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_TestExecutionSentTestRunnerProcessStartInfo_when_it_handles_the_message_and_current_state_is_InitialState()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.InitialState);

            var nextState = _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                    dotnetTestMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo);
        }

        [Fact]
        public void It_returns_TestExecutionSentTestRunnerProcessStartInfo_when_it_handles_the_message_and_current_state_is_VersionCheckCompleted()
        {
            var nextState = _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            nextState.Should().Be(DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo);
        }

        [Fact]
        public void It_gets_the_process_start_info_from_the_test_runner_when_it_handles_the_message()
        {
            _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _testRunnerMock.Verify(t => t.GetProcessStartInfo(), Times.Once);
        }

        [Fact]
        public void It_sends_the_process_start_info_when_it_handles_the_message()
        {
            _adapterChannelMock.Setup(r => r.Send(It.Is<Message>(m =>
                m.MessageType == TestMessageTypes.TestExecutionTestRunnerProcessStartInfo &&
                m.Payload.ToObject<ProcessStartInfo>().FileName == _testStartInfo.FileName &&
                m.Payload.ToObject<ProcessStartInfo>().Arguments == _testStartInfo.Arguments))).Verifiable();

            _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _adapterChannelMock.Verify();
        }

        [Fact]
        public void It_creates_a_new_reporting_channel()
        {
            _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _reportingChannelFactoryMock.Verify(r => r.CreateTestRunnerChannel(), Times.Once);
        }

        [Fact]
        public void It_calls_accept_on_the_test_runner_channel()
        {
            _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _testRunnerChannelMock.Verify(t => t.Accept(), Times.Once);
        }

        [Fact]
        public void It_makes_dotnet_test_listen_on_the_test_runner_port_for_messages_when_it_handles_the_message()
        {
            _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _dotnetTestMock.Verify(d => d.StartListeningTo(_testRunnerChannelMock.Object), Times.Once);
        }

        [Fact]
        public void It_sets_the_TestsToRun_of_DotnetTest()
        {
            _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _dotnetTestMock.VerifySet(d => d.TestsToRun = _testsToRun);
        }

        [Fact]
        public void It_passes_the_right_arguments_to_the_run_tests_arguments_builder()
        {
            _testGetTestRunnerProcessStartInfoMessageHandler.HandleMessage(
                _dotnetTestMock.Object,
                _validMessage);

            _argumentsBuilder.Should().NotBeNull();

            var arguments = _argumentsBuilder.BuildArguments();

            arguments.Should().Contain("--port", $"{TestRunnerPort}");
            arguments.Should().Contain($"{AssemblyUnderTest}");
        }
    }
}
