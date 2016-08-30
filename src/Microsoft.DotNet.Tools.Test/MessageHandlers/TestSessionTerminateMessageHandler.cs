// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestSessionTerminateMessageHandler : IDotnetTestMessageHandler
    {
        private readonly ITestMessagesCollection _messages;

        public TestSessionTerminateMessageHandler(ITestMessagesCollection messages)
        {
            _messages = messages;
        }

        public DotnetTestState HandleMessage(IDotnetTest dotnetTest, Message message)
        {
            var nextState = DotnetTestState.NoOp;

            if (TestMessageTypes.TestSessionTerminate.Equals(message.MessageType))
            {
                nextState = DotnetTestState.Terminated;
                _messages.Drain();
            }

            return nextState;
        }
    }
}
