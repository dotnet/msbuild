// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Tools.Test;

namespace Microsoft.DotNet.Tools.Test
{
    public static class DotnetTestExtensions
    {
        public static IDotnetTest AddNonSpecificMessageHandlers(
            this IDotnetTest dotnetTest,
            ITestMessagesCollection messages,
            IReportingChannel adapterChannel)
        {
            dotnetTest.TestSessionTerminateMessageHandler = new TestSessionTerminateMessageHandler(messages);
            dotnetTest.UnknownMessageHandler = new UnknownMessageHandler(adapterChannel);

            dotnetTest.AddMessageHandler(new VersionCheckMessageHandler(adapterChannel));

            return dotnetTest;
        }

        public static IDotnetTest AddTestDiscoveryMessageHandlers(
            this IDotnetTest dotnetTest,
            IReportingChannel adapterChannel,
            IReportingChannelFactory reportingChannelFactory,
            ITestRunnerFactory testRunnerFactory)
        {
            dotnetTest.AddMessageHandler(
                new TestDiscoveryStartMessageHandler(testRunnerFactory, adapterChannel, reportingChannelFactory));

            return dotnetTest;
        }

        public static IDotnetTest AddTestRunMessageHandlers(
            this IDotnetTest dotnetTest,
            IReportingChannel adapterChannel,
            IReportingChannelFactory reportingChannelFactory,
            ITestRunnerFactory testRunnerFactory)
        {
            dotnetTest.AddMessageHandler(new GetTestRunnerProcessStartInfoMessageHandler(
                testRunnerFactory,
                adapterChannel,
                reportingChannelFactory));

            return dotnetTest;
        }

        public static IDotnetTest AddTestRunnnersMessageHandlers(
            this IDotnetTest dotnetTest,
            IReportingChannel adapterChannel,
            IReportingChannelFactory reportingChannelFactory)
        {
            dotnetTest.AddMessageHandler(new TestRunnerTestStartedMessageHandler(adapterChannel));
            dotnetTest.AddMessageHandler(new TestRunnerTestResultMessageHandler(adapterChannel));
            dotnetTest.AddMessageHandler(new TestRunnerTestFoundMessageHandler(adapterChannel));
            dotnetTest.AddMessageHandler(new TestRunnerTestCompletedMessageHandler(adapterChannel));
            dotnetTest.AddMessageHandler(new TestRunnerWaitingCommandMessageHandler(reportingChannelFactory));

            return dotnetTest;
        }
    }
}
