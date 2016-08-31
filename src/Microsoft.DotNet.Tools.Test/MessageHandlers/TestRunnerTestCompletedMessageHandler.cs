// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunnerTestCompletedMessageHandler : IDotnetTestMessageHandler
    {
        private readonly IReportingChannel _adapterChannel;

        public TestRunnerTestCompletedMessageHandler(IReportingChannel adapterChannel)
        {
            _adapterChannel = adapterChannel;
        }

        public DotnetTestState HandleMessage(IDotnetTest dotnetTest, Message message)
        {
            var nextState = DotnetTestState.NoOp;
            if (CanHandleMessage(dotnetTest, message))
            {
                DoHandleMessage(dotnetTest, message);
                nextState = NextState(dotnetTest);
            }

            return nextState;
        }

        private void DoHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            _adapterChannel.Send(new Message
            {
                MessageType = MessageType(dotnetTest)
            });
        }

        private string MessageType(IDotnetTest dotnetTest)
        {
            return dotnetTest.State == DotnetTestState.TestDiscoveryStarted
                ? TestMessageTypes.TestDiscoveryCompleted
                : TestMessageTypes.TestExecutionCompleted;
        }

        private DotnetTestState NextState(IDotnetTest dotnetTest)
        {
            return dotnetTest.State == DotnetTestState.TestDiscoveryStarted
                ? DotnetTestState.TestDiscoveryCompleted
                : DotnetTestState.TestExecutionCompleted;
        }

        private bool CanHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            return IsAtAnAcceptableState(dotnetTest) && CanAcceptMessage(message);
        }

        private static bool CanAcceptMessage(Message message)
        {
            return message.MessageType == TestMessageTypes.TestRunnerTestCompleted;
        }

        private static bool IsAtAnAcceptableState(IDotnetTest dotnetTest)
        {
            return (dotnetTest.State == DotnetTestState.TestDiscoveryStarted ||
                    dotnetTest.State == DotnetTestState.TestExecutionStarted);
        }
    }
}
