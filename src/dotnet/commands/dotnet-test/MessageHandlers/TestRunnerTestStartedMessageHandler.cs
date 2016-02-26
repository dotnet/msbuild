// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunnerTestStartedMessageHandler : TestRunnerResultMessageHandler
    {
        public TestRunnerTestStartedMessageHandler(IReportingChannel adapterChannel)
            : base(adapterChannel, DotnetTestState.TestExecutionStarted, TestMessageTypes.TestExecutionStarted)
        {
        }

        protected override bool CanHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            return IsAtAnAcceptableState(dotnetTest) &&
                   message.MessageType == TestMessageTypes.TestRunnerTestStarted;
        }

        private static bool IsAtAnAcceptableState(IDotnetTest dotnetTest)
        {
            return dotnetTest.State == DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo ||
                dotnetTest.State == DotnetTestState.TestExecutionStarted;
        }
    }
}
