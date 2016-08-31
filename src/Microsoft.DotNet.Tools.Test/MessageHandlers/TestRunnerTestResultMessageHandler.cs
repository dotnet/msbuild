// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunnerTestResultMessageHandler : TestRunnerResultMessageHandler
    {
        public TestRunnerTestResultMessageHandler(IReportingChannel adapterChannel)
            : base(adapterChannel, DotnetTestState.TestExecutionStarted, TestMessageTypes.TestExecutionTestResult)
        {
        }

        protected override bool CanHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            return dotnetTest.State == DotnetTestState.TestExecutionStarted &&
                   message.MessageType == TestMessageTypes.TestRunnerTestResult;
        }
    }
}
