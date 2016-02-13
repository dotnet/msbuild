// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunnerTestFoundMessageHandler : TestRunnerResultMessageHandler
    {
        public TestRunnerTestFoundMessageHandler(IReportingChannel adapterChannel)
            : base(adapterChannel, DotnetTestState.TestDiscoveryStarted, TestMessageTypes.TestDiscoveryTestFound)
        {
        }

        protected override bool CanHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            return dotnetTest.State == DotnetTestState.TestDiscoveryStarted &&
                   message.MessageType == TestMessageTypes.TestRunnerTestFound;
        }
    }
}
