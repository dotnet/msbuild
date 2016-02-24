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
    public class GivenAVersionCheckMessageHandler
    {
        private Mock<IReportingChannel> _reportingChannelMock;
        private VersionCheckMessageHandler _versionCheckMessageHandler;
        private Message _validMessage;
        private IDotnetTest _dotnetTestAtInitialState;

        public GivenAVersionCheckMessageHandler()
        {
            _reportingChannelMock = new Mock<IReportingChannel>();
            _versionCheckMessageHandler = new VersionCheckMessageHandler(_reportingChannelMock.Object);

            _validMessage = new Message
            {
                MessageType = TestMessageTypes.VersionCheck,
                Payload = JToken.FromObject(new ProtocolVersionMessage
                {
                    Version = 99
                })
            };

            var dotnetTestAtInitialStateMock = new Mock<IDotnetTest>();
            dotnetTestAtInitialStateMock.Setup(d => d.State).Returns(DotnetTestState.InitialState);
            _dotnetTestAtInitialState = dotnetTestAtInitialStateMock.Object;
        }

        [Fact]
        public void It_returns_NoOp_if_the_dotnet_test_state_is_not_initial()
        {
            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.Terminated);

            var nextState = _versionCheckMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                new Message {MessageType = TestMessageTypes.VersionCheck});

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_NoOp_if_the_message_is_not_VersionCheck()
        {
            var nextState = _versionCheckMessageHandler.HandleMessage(
                _dotnetTestAtInitialState,
                new Message { MessageType = "Something different from ProtocolVersion" });

            nextState.Should().Be(DotnetTestState.NoOp);
        }

        [Fact]
        public void It_returns_VersionCheckCompleted_when_it_handles_the_message()
        {
            var nextState = _versionCheckMessageHandler.HandleMessage(_dotnetTestAtInitialState, _validMessage);

            nextState.Should().Be(DotnetTestState.VersionCheckCompleted);
        }

        [Fact]
        public void It_returns_a_ProtocolVersion_with_the_SupportedVersion_when_it_handles_the_message()
        {
            _reportingChannelMock.Setup(r =>
                r.Send(It.Is<Message>(m =>
                m.MessageType == TestMessageTypes.VersionCheck &&
                m.Payload.ToObject<ProtocolVersionMessage>().Version == 1))).Verifiable();

            _versionCheckMessageHandler.HandleMessage(_dotnetTestAtInitialState, _validMessage);

            _reportingChannelMock.Verify();
        }
    }
}
