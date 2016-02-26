// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenThatWeWantToDiscoverTests
    {
        [Fact]
        public void Dotnet_test_handles_and_sends_all_the_right_messages()
        {
            var dotnetTestMessageScenario = new DotnetTestMessageScenario();

            dotnetTestMessageScenario.TestRunnerMock
                .Setup(t => t.RunTestCommand())
                .Callback(() => dotnetTestMessageScenario.TestRunnerChannelMock.Raise(
                    t => t.MessageReceived += null,
                    dotnetTestMessageScenario.DotnetTestUnderTest,
                    new Message
                    {
                        MessageType = TestMessageTypes.TestRunnerTestFound,
                        Payload = JToken.FromObject("testFound")
                    }))
                .Verifiable();

            dotnetTestMessageScenario.AdapterChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.VersionCheck)))
                .Callback(() => dotnetTestMessageScenario.AdapterChannelMock.Raise(
                    r => r.MessageReceived += null,
                    dotnetTestMessageScenario.DotnetTestUnderTest,
                    new Message
                    {
                        MessageType = TestMessageTypes.TestDiscoveryStart
                    }))
                .Verifiable();

            dotnetTestMessageScenario.AdapterChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.TestDiscoveryTestFound)))
                .Callback(() => dotnetTestMessageScenario.TestRunnerChannelMock.Raise(
                    t => t.MessageReceived += null,
                    dotnetTestMessageScenario.DotnetTestUnderTest,
                    new Message
                    {
                        MessageType = TestMessageTypes.TestRunnerTestCompleted
                    }))
                .Verifiable();

            dotnetTestMessageScenario.AdapterChannelMock
                .Setup(a => a.Send(It.Is<Message>(m => m.MessageType == TestMessageTypes.TestDiscoveryCompleted)))
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
