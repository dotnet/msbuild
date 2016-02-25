// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Tools.Test;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenATestDiscoveryStartMessageHandler
    {
        private const int TestRunnerPort = 1;
        private const string AssemblyUnderTest = "assembly.dll";

        private TestDiscoveryStartMessageHandler _testDiscoveryStartMessageHandler;
        private IDotnetTest _dotnetTestAtVersionCheckCompletedState;
        private Message _validMessage;
        private Mock<ITestRunnerFactory> _testRunnerFactoryMock;
        private Mock<ITestRunner> _testRunnerMock;
        private Mock<IReportingChannel> _adapterChannelMock;
        private Mock<IReportingChannel> _testRunnerChannelMock;
        private Mock<IReportingChannelFactory> _reportingChannelFactoryMock;
        private DiscoverTestsArgumentsBuilder _argumentsBuilder;
        private Mock<IDotnetTest> _dotnetTestMock;

        public GivenATestDiscoveryStartMessageHandler()
        {
            _dotnetTestMock = new Mock<IDotnetTest>();
            _dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.VersionCheckCompleted);
            _dotnetTestMock.Setup(d => d.PathToAssemblyUnderTest).Returns(AssemblyUnderTest);
            _dotnetTestAtVersionCheckCompletedState = _dotnetTestMock.Object;

            _testRunnerMock = new Mock<ITestRunner>();
            _testRunnerFactoryMock = new Mock<ITestRunnerFactory>();
            _testRunnerFactoryMock
                .Setup(c => c.CreateTestRunner(It.IsAny<DiscoverTestsArgumentsBuilder>()))
                .Callback<ITestRunnerArgumentsBuilder>(r => _argumentsBuilder = r as DiscoverTestsArgumentsBuilder)
                .Returns(_testRunnerMock.Object);

            _adapterChannelMock = new Mock<IReportingChannel>();

            _testRunnerChannelMock = new Mock<IReportingChannel>();
            _testRunnerChannelMock.Setup(t => t.Port).Returns(TestRunnerPort);

            _reportingChannelFactoryMock = new Mock<IReportingChannelFactory>();
            _reportingChannelFactoryMock.Setup(r =>
                r.CreateChannelWithAnyAvailablePort()).Returns(_testRunnerChannelMock.Object);

            _testDiscoveryStartMessageHandler = new TestDiscoveryStartMessageHandler(
                _testRunnerFactoryMock.Object,
                _adapterChannelMock.Object,
                _reportingChannelFactoryMock.Object);

            _validMessage = new Message
            {
                MessageType = TestMessageTypes.TestDiscoveryStart
            };
        }

        [Fact]
        public void It_returns_NoOp_if_the_dotnet_test_state_is_not_VersionCheckCompleted_or_InitialState()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.Terminated);

            var nextState = _testDiscoveryStartMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                new Message { MessageType = TestMessageTypes.TestDiscoveryStart });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_TestDiscoveryStart()
        {
            var nextState = _testDiscoveryStartMessageHandler.HandleMessage(
                _dotnetTestAtVersionCheckCompletedState,
                new Message { MessageType = "Something different from TestDiscovery.Start" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_TestDiscoveryCompleted_when_it_handles_the_message_and_current_state_is_InitialState()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.InitialState);

            var nextState =
                _testDiscoveryStartMessageHandler.HandleMessage(dotnetTestMock.Object, _validMessage);

            nextState.Should().Be(DotnetTestState.TestDiscoveryStarted);
        }

        [Fact]
        public void It_returns_TestDiscoveryCompleted_when_it_handles_the_message_and_current_state_is_VersionCheckCompleted()
        {
            var nextState =
                _testDiscoveryStartMessageHandler.HandleMessage(_dotnetTestAtVersionCheckCompletedState, _validMessage);

            nextState.Should().Be(DotnetTestState.TestDiscoveryStarted);
        }

        [Fact]
        public void It_uses_the_test_runner_to_discover_tests_when_it_handles_the_message()
        {
            _testDiscoveryStartMessageHandler.HandleMessage(_dotnetTestAtVersionCheckCompletedState, _validMessage);

            _testRunnerMock.Verify(t => t.RunTestCommand(), Times.Once);
        }

        [Fact]
        public void It_sends_an_error_when_the_test_runner_fails()
        {
            const string testRunner = "SomeTestRunner";

            _testRunnerMock.Setup(t => t.RunTestCommand()).Throws(new TestRunnerOperationFailedException(testRunner, 1));

            _testDiscoveryStartMessageHandler.HandleMessage(_dotnetTestAtVersionCheckCompletedState, _validMessage);

            _adapterChannelMock.Verify(r => r.SendError($"'{testRunner}' returned '1'."), Times.Once);
        }

        [Fact]
        public void It_creates_a_new_reporting_channel()
        {
            _testDiscoveryStartMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _reportingChannelFactoryMock.Verify(r => r.CreateChannelWithAnyAvailablePort(), Times.Once);
        }

        [Fact]
        public void It_calls_accept_on_the_test_runner_channel()
        {
            _testDiscoveryStartMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _testRunnerChannelMock.Verify(t => t.Accept(), Times.Once);
        }

        [Fact]
        public void It_makes_dotnet_test_listen_on_the_test_runner_port_for_messages_when_it_handles_the_message()
        {
            _testDiscoveryStartMessageHandler.HandleMessage(
                    _dotnetTestMock.Object,
                    _validMessage);

            _dotnetTestMock.Verify(d => d.StartListeningTo(_testRunnerChannelMock.Object), Times.Once);
        }

        [Fact]
        public void It_passes_the_right_arguments_to_the_run_tests_arguments_builder()
        {
            _testDiscoveryStartMessageHandler.HandleMessage(
                _dotnetTestMock.Object,
                _validMessage);

            _argumentsBuilder.Should().NotBeNull();

            var arguments = _argumentsBuilder.BuildArguments();

            arguments.Should().Contain("--port", $"{TestRunnerPort}");
            arguments.Should().Contain($"{AssemblyUnderTest}");
            arguments.Should().Contain("--list");
            arguments.Should().Contain("--designtime");
        }
    }
}
