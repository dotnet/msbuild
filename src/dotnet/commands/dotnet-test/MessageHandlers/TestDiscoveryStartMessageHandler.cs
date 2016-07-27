// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Cli.Tools.Test
{
    public class TestDiscoveryStartMessageHandler : IDotnetTestMessageHandler
    {
        private readonly ITestRunnerFactory _testRunnerFactory;
        private readonly IReportingChannel _adapterChannel;
        private readonly IReportingChannelFactory _reportingChannelFactory;

        public TestDiscoveryStartMessageHandler(
            ITestRunnerFactory testRunnerFactory,
            IReportingChannel adapterChannel,
            IReportingChannelFactory reportingChannelFactory)
        {
            _testRunnerFactory = testRunnerFactory;
            _adapterChannel = adapterChannel;
            _reportingChannelFactory = reportingChannelFactory;
        }

        public DotnetTestState HandleMessage(IDotnetTest dotnetTest, Message message)
        {
            var nextState = DotnetTestState.NoOp;
            if (CanHandleMessage(dotnetTest, message))
            {
                HandleMessage(dotnetTest);
                nextState = DotnetTestState.TestDiscoveryStarted;
            }

            return nextState;
        }

        private void HandleMessage(IDotnetTest dotnetTest)
        {
            TestHostTracing.Source.TraceInformation("Starting Discovery");

            DiscoverTests(dotnetTest);
        }

        private void DiscoverTests(IDotnetTest dotnetTest)
        {
            var testRunnerResults = Enumerable.Empty<Message>();

            try
            {
                var testRunnerChannel = _reportingChannelFactory.CreateTestRunnerChannel();

                dotnetTest.StartListeningTo(testRunnerChannel);

                testRunnerChannel.Connect();

                var testRunner = _testRunnerFactory.CreateTestRunner(
                    new DiscoverTestsArgumentsBuilder(dotnetTest.PathToAssemblyUnderTest, testRunnerChannel.Port));

                testRunner.RunTestCommand();
            }
            catch (TestRunnerOperationFailedException e)
            {
                _adapterChannel.SendError(e.Message);
            }
        }

        private static bool CanHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            return IsAtAnAcceptableState(dotnetTest) && message.MessageType == TestMessageTypes.TestDiscoveryStart;
        }

        private static bool IsAtAnAcceptableState(IDotnetTest dotnetTest)
        {
            return (dotnetTest.State == DotnetTestState.VersionCheckCompleted ||
                dotnetTest.State == DotnetTestState.InitialState);
        }
    }
}
