// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class DotnetTest : IDotnetTest
    {
        private readonly IList<IReportingChannel> _channels;
        private readonly IList<IDotnetTestMessageHandler> _messageHandlers;
        private readonly ITestMessagesCollection _messages;

        public IDotnetTestMessageHandler TestSessionTerminateMessageHandler { private get; set; }
        public IDotnetTestMessageHandler UnknownMessageHandler { private get; set; }

        public DotnetTestState State { get; private set; }

        public string PathToAssemblyUnderTest { get; }

        public DotnetTest(ITestMessagesCollection messages, string pathToAssemblyUnderTest)
        {
            PathToAssemblyUnderTest = pathToAssemblyUnderTest;
            State = DotnetTestState.InitialState;
            _channels = new List<IReportingChannel>();
            _messageHandlers = new List<IDotnetTestMessageHandler>();
            _messages = messages;
        }

        public DotnetTest AddMessageHandler(IDotnetTestMessageHandler messageHandler)
        {
            _messageHandlers.Add(messageHandler);

            return this;
        }

        public void StartHandlingMessages()
        {
            Message message;
            while (_messages.TryTake(out message))
            {
                HandleMessage(message);
            }
        }

        public void StartListeningTo(IReportingChannel reportingChannel)
        {
            ValidateSpecialMessageHandlersArePresent();

            _channels.Add(reportingChannel);
            reportingChannel.MessageReceived += OnMessageReceived;
        }

        public void Dispose()
        {
            foreach (var reportingChannel in _channels)
            {
                reportingChannel.Dispose();
            }
        }

        private void ValidateSpecialMessageHandlersArePresent()
        {
            if (TestSessionTerminateMessageHandler == null)
            {
                throw new InvalidOperationException("The TestSession.Terminate message handler needs to be set.");
            }

            if (UnknownMessageHandler == null)
            {
                throw new InvalidOperationException("The unknown message handler needs to be set.");
            }
        }

        private void HandleMessage(Message message)
        {
            foreach (var messageHandler in _messageHandlers)
            {
                var nextState = messageHandler.HandleMessage(this, message);

                if (nextState != DotnetTestState.NoOp)
                {
                    State = nextState;
                    return;
                }
            }

            UnknownMessageHandler.HandleMessage(this, message);
        }

        private void OnMessageReceived(object sender, Message message)
        {
            if (!TerminateTestSession(message))
            {
                _messages.Add(message);
            }
        }

        private bool TerminateTestSession(Message message)
        {
            return TestSessionTerminateMessageHandler.HandleMessage(this, message) == DotnetTestState.Terminated;
        }
    }
}
