// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test
{
    public class DesignTimeRunner : IDotnetTestRunner
    {
        private readonly ITestRunnerResolver _testRunnerResolver;

        private readonly ICommandFactory _commandFactory;

        private readonly string _assemblyUnderTest;

        public DesignTimeRunner(
            ITestRunnerResolver testRunnerResolver,
            ICommandFactory commandFactory,
            string assemblyUnderTest)
        {
            _testRunnerResolver = testRunnerResolver;
            _commandFactory = commandFactory;
            _assemblyUnderTest = assemblyUnderTest;
        }

        public int RunTests(DotnetTestParams dotnetTestParams)
        {
            Console.WriteLine("Listening on port {0}", dotnetTestParams.Port.Value);

            HandleDesignTimeMessages(dotnetTestParams);

            return 0;
        }

        private void HandleDesignTimeMessages(DotnetTestParams dotnetTestParams)
        {
            var reportingChannelFactory = new ReportingChannelFactory();
            var adapterChannel = reportingChannelFactory.CreateAdapterChannel(dotnetTestParams.Port.Value);

            try
            {
                var pathToAssemblyUnderTest = _assemblyUnderTest;
                var messages = new TestMessagesCollection();
                using (var dotnetTest = new DotnetTest(messages, pathToAssemblyUnderTest))
                {
                    var testRunnerFactory =
                        new TestRunnerFactory(_testRunnerResolver.ResolveTestRunner(), _commandFactory);

                    dotnetTest
                        .AddNonSpecificMessageHandlers(messages, adapterChannel)
                        .AddTestDiscoveryMessageHandlers(adapterChannel, reportingChannelFactory, testRunnerFactory)
                        .AddTestRunMessageHandlers(adapterChannel, reportingChannelFactory, testRunnerFactory)
                        .AddTestRunnnersMessageHandlers(adapterChannel, reportingChannelFactory);

                    dotnetTest.StartListeningTo(adapterChannel);

                    adapterChannel.Connect();

                    dotnetTest.StartHandlingMessages();
                }
            }
            catch (Exception ex)
            {
                adapterChannel.SendError(ex);
            }
        }
    }
}
