// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Testing.Abstractions;
using Newtonsoft.Json.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunnerWaitingCommandMessageHandler : IDotnetTestMessageHandler
    {
        private readonly IReportingChannelFactory _reportingChannelFactory;
        private IReportingChannel _testRunnerChannel;

        public TestRunnerWaitingCommandMessageHandler(IReportingChannelFactory reportingChannelFactory)
        {
            _reportingChannelFactory = reportingChannelFactory;

            _reportingChannelFactory.TestRunnerChannelCreated += OnTestRunnerChannelCreated;
        }

        public DotnetTestState HandleMessage(IDotnetTest dotnetTest, Message message)
        {
            var nextState = DotnetTestState.NoOp;

            if (CanHandleMessage(dotnetTest, message))
            {
                HandleMessage(dotnetTest);
                nextState = DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo;
            }

            return nextState;
        }

        private void HandleMessage(IDotnetTest dotnetTest)
        {
            if (_testRunnerChannel == null)
            {
                const string errorMessage =
                    "A test runner channel hasn't been created for TestRunnerWaitingCommandMessageHandler";
                throw new InvalidOperationException(errorMessage);
            }

            _testRunnerChannel.Send(new Message
            {
                MessageType = TestMessageTypes.TestRunnerExecute,
                Payload = JToken.FromObject(new RunTestsMessage
                {
                    Tests = new List<string>(dotnetTest.TestsToRun.OrEmptyIfNull())
                })
            });
        }

        private void OnTestRunnerChannelCreated(object sender, IReportingChannel testRunnerChannel)
        {
            if (_testRunnerChannel != null)
            {
                const string errorMessage = "TestRunnerWaitingCommandMessageHandler already has a test runner channel";
                throw new InvalidOperationException(errorMessage);
            }

            _testRunnerChannel = testRunnerChannel;
        }

        private static bool CanHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            return dotnetTest.State == DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo &&
                message.MessageType == TestMessageTypes.TestRunnerWaitingCommand;
        }
    }
}
