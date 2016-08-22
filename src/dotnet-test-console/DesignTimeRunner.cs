// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public class DesignTimeRunner : BaseDotnetTestRunner
    {
        internal override int DoRunTests(ProjectContext projectContext, DotnetTestParams dotnetTestParams)
        {
            Console.WriteLine("Listening on port {0}", dotnetTestParams.Port.Value);

            HandleDesignTimeMessages(projectContext, dotnetTestParams);

            return 0;
        }

        private static void HandleDesignTimeMessages(
            ProjectContext projectContext,
            DotnetTestParams dotnetTestParams)
        {
            var reportingChannelFactory = new ReportingChannelFactory();
            var adapterChannel = reportingChannelFactory.CreateAdapterChannel(dotnetTestParams.Port.Value);

            try
            {
                var pathToAssemblyUnderTest = new AssemblyUnderTest(projectContext, dotnetTestParams).Path;
                var messages = new TestMessagesCollection();
                using (var dotnetTest = new DotnetTest(messages, pathToAssemblyUnderTest))
                {
                    var commandFactory =
                        new ProjectDependenciesCommandFactory(
                            projectContext.TargetFramework,
                            dotnetTestParams.Config,
                            dotnetTestParams.Output,
                            dotnetTestParams.BuildBasePath,
                            projectContext.ProjectDirectory);

                    var testRunnerFactory =
                        new TestRunnerFactory(GetCommandName(projectContext.ProjectFile.TestRunner), commandFactory);

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

        private static string GetCommandName(string testRunner)
        {
            return $"dotnet-test-{testRunner}";
        }
    }
}
