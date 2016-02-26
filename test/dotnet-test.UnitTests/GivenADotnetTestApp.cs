// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenADotnetTestApp
    {
        private const string AssemblyUnderTest = "assembly.dll";

        private Mock<IReportingChannel> _reportingChannelMock;
        private Mock<IDotnetTestMessageHandler> _noOpMessageHandlerMock;
        private Mock<IDotnetTestMessageHandler> _realMessageHandlerMock;
        private Mock<IDotnetTestMessageHandler> _unknownMessageHandlerMock;
        private DotnetTest _dotnetTest;

        public GivenADotnetTestApp()
        {
            _noOpMessageHandlerMock = new Mock<IDotnetTestMessageHandler>();
            _noOpMessageHandlerMock
                .Setup(mh => mh.HandleMessage(It.IsAny<DotnetTest>(), It.IsAny<Message>()))
                .Returns(DotnetTestState.NoOp)
                .Verifiable();

            _realMessageHandlerMock = new Mock<IDotnetTestMessageHandler>();
            _realMessageHandlerMock
                .Setup(mh => mh.HandleMessage(It.IsAny<DotnetTest>(), It.Is<Message>(m => m.MessageType == "Test message")))
                .Returns(DotnetTestState.VersionCheckCompleted).Callback(() =>
                _reportingChannelMock.Raise(r => r.MessageReceived += null, _dotnetTest, new Message
                {
                    MessageType = TestMessageTypes.TestSessionTerminate
                }));

            _reportingChannelMock = new Mock<IReportingChannel>();
            _unknownMessageHandlerMock = new Mock<IDotnetTestMessageHandler>();
            _unknownMessageHandlerMock
                .Setup(mh => mh.HandleMessage(It.IsAny<DotnetTest>(), It.IsAny<Message>()))
                .Throws<InvalidOperationException>();

            var testMessagesCollection = new TestMessagesCollection();
            _dotnetTest = new DotnetTest(testMessagesCollection, AssemblyUnderTest)
            {
                TestSessionTerminateMessageHandler = new TestSessionTerminateMessageHandler(testMessagesCollection),
                UnknownMessageHandler = _unknownMessageHandlerMock.Object
            };

            _dotnetTest.StartListeningTo(_reportingChannelMock.Object);

            _reportingChannelMock.Raise(r => r.MessageReceived += null, _dotnetTest, new Message
            {
                MessageType = "Test message"
            });
        }

        [Fact]
        public void DotnetTest_handles_TestSession_Terminate_messages_implicitly()
        {
            _reportingChannelMock.Raise(r => r.MessageReceived += null, _dotnetTest, new Message
            {
                MessageType = TestMessageTypes.TestSessionTerminate
            });

            _dotnetTest.StartHandlingMessages();

            //just the fact that we are not hanging means we stopped waiting for messages
        }

        [Fact]
        public void DotnetTest_calls_each_MessageHandler_until_one_returns_a_state_different_from_NoOp()
        {
            var secondNoOpMessageHandler = new Mock<IDotnetTestMessageHandler>();

            _dotnetTest
                .AddMessageHandler(_noOpMessageHandlerMock.Object)
                .AddMessageHandler(_realMessageHandlerMock.Object)
                .AddMessageHandler(secondNoOpMessageHandler.Object);

            _dotnetTest.StartHandlingMessages();

            _noOpMessageHandlerMock.Verify();
            _realMessageHandlerMock.Verify();
            secondNoOpMessageHandler.Verify(
                mh => mh.HandleMessage(It.IsAny<DotnetTest>(), It.IsAny<Message>()),
                Times.Never);
        }

        [Fact]
        public void DotnetTest_does_not_send_an_error_when_the_message_gets_handled()
        {
            _dotnetTest.AddMessageHandler(_realMessageHandlerMock.Object);

            _dotnetTest.StartHandlingMessages();

            _reportingChannelMock.Verify(r => r.SendError(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void DotnetTest_calls_the_unknown_message_handler_when_the_message_is_not_handled()
        {
            _dotnetTest.AddMessageHandler(_noOpMessageHandlerMock.Object);

            Action action = () => _dotnetTest.StartHandlingMessages();

            action.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void It_throws_an_InvalidOperationException_if_StartListening_is_called_without_setting_a_TestSessionTerminateMessageHandler()
        {
            var dotnetTest = new DotnetTest(new TestMessagesCollection(), AssemblyUnderTest)
            {
                UnknownMessageHandler = new Mock<IDotnetTestMessageHandler>().Object
            };

            Action action = () => dotnetTest.StartListeningTo(new Mock<IReportingChannel>().Object);

            action.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void It_throws_an_InvalidOperationException_if_StartListeningTo_is_called_without_setting_a_UnknownMessageHandler()
        {
            var dotnetTest = new DotnetTest(new TestMessagesCollection(), AssemblyUnderTest)
            {
                TestSessionTerminateMessageHandler = new Mock<IDotnetTestMessageHandler>().Object
            };

            Action action = () => dotnetTest.StartListeningTo(new Mock<IReportingChannel>().Object);

            action.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void It_disposes_all_reporting_channels_that_it_was_listening_to_when_it_gets_disposed()
        {
            var firstReportingChannelMock = new Mock<IReportingChannel>();
            var secondReportingChannelMock = new Mock<IReportingChannel>();
            using (var dotnetTest = new DotnetTest(new TestMessagesCollection(), AssemblyUnderTest))
            {
                dotnetTest.TestSessionTerminateMessageHandler = new Mock<IDotnetTestMessageHandler>().Object;
                dotnetTest.UnknownMessageHandler = new Mock<IDotnetTestMessageHandler>().Object;

                dotnetTest.StartListeningTo(firstReportingChannelMock.Object);
                dotnetTest.StartListeningTo(secondReportingChannelMock.Object);
            }

            firstReportingChannelMock.Verify(r => r.Dispose(), Times.Once);
            secondReportingChannelMock.Verify(r => r.Dispose(), Times.Once);
        }
    }
}
