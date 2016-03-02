// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenThatWeWantToRunTests
    {
        [Fact]
        public void Dotnet_test_handles_and_sends_all_the_right_messages()
        {
            var dotnetTestMessageScenario = new DotnetTestMessageScenario();

            dotnetTestMessageScenario.TestRunnerMock
                .Setup(t => t.GetProcessStartInfo())
                .Returns(new TestStartInfo())
                .Verifiable();

            dotnetTestMessageScenario.AdapterChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.VersionCheck)))
                .Callback(() => dotnetTestMessageScenario.AdapterChannelMock.Raise(
                    r => r.MessageReceived += null,
                    dotnetTestMessageScenario.DotnetTestUnderTest,
                    new Message
                    {
                        MessageType = TestMessageTypes.TestExecutionGetTestRunnerProcessStartInfo
                    }))
                .Verifiable();

            dotnetTestMessageScenario.AdapterChannelMock
                .Setup(a => a.Send(
                    It.Is<Message>(m => m.MessageType == TestMessageTypes.TestExecutionTestRunnerProcessStartInfo)))
                .Callback(() => dotnetTestMessageScenario.TestRunnerChannelMock.Raise(
                    t => t.MessageReceived += null,
                    dotnetTestMessageScenario.DotnetTestUnderTest,
                    new Message
                    {
                        MessageType = TestMessageTypes.TestRunnerTestStarted
                    }))
                .Verifiable();

            dotnetTestMessageScenario.AdapterChannelMock
                .Setup(a => a.Send(
                    It.Is<Message>(m => m.MessageType == TestMessageTypes.TestExecutionStarted)))
                .Callback(() => dotnetTestMessageScenario.TestRunnerChannelMock.Raise(
                    t => t.MessageReceived += null,
                    dotnetTestMessageScenario.DotnetTestUnderTest,
                    new Message
                    {
                        MessageType = TestMessageTypes.TestRunnerTestResult
                    }))
                .Verifiable();

            dotnetTestMessageScenario.AdapterChannelMock
                .Setup(a => a.Send(
                    It.Is<Message>(m => m.MessageType == TestMessageTypes.TestExecutionTestResult)))
                .Callback(() => dotnetTestMessageScenario.TestRunnerChannelMock.Raise(
                    t => t.MessageReceived += null,
                    dotnetTestMessageScenario.DotnetTestUnderTest,
                    new Message
                    {
                        MessageType = TestMessageTypes.TestRunnerTestCompleted
                    }))
                .Verifiable();

            dotnetTestMessageScenario.AdapterChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.TestExecutionCompleted)))
                .Callback(() => dotnetTestMessageScenario.AdapterChannelMock.Raise(
                    r => r.MessageReceived += null,
                    dotnetTestMessageScenario.DotnetTestUnderTest,
                    new Message
                    {
                        MessageType = TestMessageTypes.TestSessionTerminate
                    }))
                .Verifiable();

            dotnetTestMessageScenario.Run();
        }
    }
}
