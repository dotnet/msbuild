// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenAUnknownMessageHandler
    {
        [Fact]
        public void It_throws_InvalidOperationException_and_sends_an_error_when_the_message_is_not_handled()
        {
            const string expectedError = "No handler for message 'Test Message' when at state 'InitialState'";

            var dotnetTestMock = new Mock<IDotnetTest>();
            dotnetTestMock.Setup(d => d.State).Returns(DotnetTestState.InitialState);

            var reportingChannel = new Mock<IReportingChannel>();
            reportingChannel.Setup(r => r.SendError(expectedError)).Verifiable();

            var unknownMessageHandler = new UnknownMessageHandler(reportingChannel.Object);

            Action action = () => unknownMessageHandler.HandleMessage(
                dotnetTestMock.Object,
                new Message { MessageType = "Test Message" });

            action.ShouldThrow<InvalidOperationException>().WithMessage(expectedError);

            reportingChannel.Verify();
        }
    }
}
