// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Tools.Test
{
    public class GetTestRunnerProcessStartInfoMessageHandler : IDotnetTestMessageHandler
    {
        private readonly ITestRunnerFactory _testRunnerFactory;
        private readonly IReportingChannel _adapterChannel;
        private readonly IReportingChannelFactory _reportingChannelFactory;

        public GetTestRunnerProcessStartInfoMessageHandler(
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
                DoHandleMessage(dotnetTest, message);
                nextState = DotnetTestState.TestExecutionSentTestRunnerProcessStartInfo;
            }

            return nextState;
        }

        private void DoHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            var testRunnerChannel = _reportingChannelFactory.CreateChannelWithAnyAvailablePort();

            dotnetTest.StartListeningTo(testRunnerChannel);

            testRunnerChannel.Accept();

            var testRunner = _testRunnerFactory.CreateTestRunner(
                new RunTestsArgumentsBuilder(dotnetTest.PathToAssemblyUnderTest, testRunnerChannel.Port, message));

            var processStartInfo = testRunner.GetProcessStartInfo();

            _adapterChannel.Send(new Message
            {
                MessageType = TestMessageTypes.TestExecutionTestRunnerProcessStartInfo,
                Payload = JToken.FromObject(processStartInfo)
            });
        }

        private static bool CanHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            return IsAtAnAcceptableState(dotnetTest) &&
                message.MessageType == TestMessageTypes.TestExecutionGetTestRunnerProcessStartInfo;
        }

        private static bool IsAtAnAcceptableState(IDotnetTest dotnetTest)
        {
            return dotnetTest.State == DotnetTestState.VersionCheckCompleted ||
                dotnetTest.State == DotnetTestState.InitialState;
        }
    }
}
