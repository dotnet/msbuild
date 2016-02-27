// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;
using Moq;
using NuGet.Frameworks;
using Xunit;
using Newtonsoft.Json;
using Microsoft.Extensions.Testing.Abstractions;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenATestRunner
    {
        private Mock<ICommand> _commandMock;
        private Mock<ICommandFactory> _commandFactoryMock;
        private Mock<ITestRunnerArgumentsBuilder> _argumentsBuilderMock;
        private string _runner = "runner";
        private string[] _testRunnerArguments;

        public GivenATestRunner()
        {
            _testRunnerArguments = new[] {"assembly.dll", "--list", "--designtime"};

            _commandMock = new Mock<ICommand>();
            _commandMock.Setup(c => c.CommandName).Returns(_runner);
            _commandMock.Setup(c => c.CommandArgs).Returns(string.Join(" ", _testRunnerArguments));
            _commandMock.Setup(c => c.OnOutputLine(It.IsAny<Action<string>>())).Returns(_commandMock.Object);

            _argumentsBuilderMock = new Mock<ITestRunnerArgumentsBuilder>();
            _argumentsBuilderMock.Setup(a => a.BuildArguments())
                .Returns(_testRunnerArguments);

            _commandFactoryMock = new Mock<ICommandFactory>();
            _commandFactoryMock.Setup(c => c.Create(
                $"dotnet-{_runner}",
                _testRunnerArguments,
                new NuGetFramework("DNXCore", Version.Parse("5.0")),
                Constants.DefaultConfiguration)).Returns(_commandMock.Object).Verifiable();
        }

        [Fact]
        public void It_creates_a_command_using_the_right_parameters()
        {
            var testRunner = new TestRunner(_runner, _commandFactoryMock.Object, _argumentsBuilderMock.Object);

            testRunner.RunTestCommand();

            _commandFactoryMock.Verify();
        }

        [Fact]
        public void It_executes_the_command()
        {
            var testRunner = new TestRunner(_runner, _commandFactoryMock.Object, _argumentsBuilderMock.Object);

            testRunner.RunTestCommand();

            _commandMock.Verify(c => c.Execute(), Times.Once);
        }

        [Fact]
        public void It_throws_TestRunnerOperationFailedException_when_the_returns_return_an_error_code()
        {
            _commandMock.Setup(c => c.Execute()).Returns(new CommandResult(null, 1, null, null));

            var testRunner = new TestRunner(_runner, _commandFactoryMock.Object, _argumentsBuilderMock.Object);

            Action action = () => testRunner.RunTestCommand();

            action.ShouldThrow<TestRunnerOperationFailedException>();
        }

        [Fact]
        public void It_executes_the_command_when_RunTestCommand_is_called()
        {
            var testResult = new Message
            {
                MessageType = "Irrelevant",
                Payload = JToken.FromObject("Irrelevant")
            };

            var testRunner = new TestRunner(_runner, _commandFactoryMock.Object, _argumentsBuilderMock.Object);

            testRunner.RunTestCommand();

            _commandMock.Verify(c => c.Execute(), Times.Once);
        }

        [Fact]
        public void It_returns_a_ProcessStartInfo_object_with_the_right_parameters_to_execute_the_test_command()
        {
            var testRunner = new TestRunner(_runner, _commandFactoryMock.Object, _argumentsBuilderMock.Object);

            var testCommandProcessStartInfo = testRunner.GetProcessStartInfo();

            testCommandProcessStartInfo.FileName.Should().Be(_runner);
            testCommandProcessStartInfo.Arguments.Should().Be(string.Join(" ", _testRunnerArguments));
        }
    }
}
