// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public abstract class TestRunnerResultMessageHandler : IDotnetTestMessageHandler
    {
        private readonly IReportingChannel _adapterChannel;
        private readonly DotnetTestState _nextStateIfHandled;
        private readonly string _messageIfHandled;

        protected TestRunnerResultMessageHandler(
            IReportingChannel adapterChannel,
            DotnetTestState nextStateIfHandled,
            string messageIfHandled)
        {
            _adapterChannel = adapterChannel;
            _nextStateIfHandled = nextStateIfHandled;
            _messageIfHandled = messageIfHandled;
        }

        public DotnetTestState HandleMessage(IDotnetTest dotnetTest, Message message)
        {
            var nextState = DotnetTestState.NoOp;
            if (CanHandleMessage(dotnetTest, message))
            {
                HandleMessage(message);
                nextState = _nextStateIfHandled;
            }

            return nextState;
        }

        private void HandleMessage(Message message)
        {
            _adapterChannel.Send(new Message
            {
                MessageType = _messageIfHandled,
                Payload = message.Payload
            });
        }

        protected abstract bool CanHandleMessage(IDotnetTest dotnetTest, Message message);
    }
}
