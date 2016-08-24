// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestCommand
    {
        private readonly IDotnetTestRunnerFactory _dotnetTestRunnerFactory;

        public static int Run(string[] args)
        {
            var dotnetTestRunnerResolverFactory = new DotnetTestRunnerResolverFactory(new ProjectReader());
            var testCommand = new TestCommand(new DotnetTestRunnerFactory(dotnetTestRunnerResolverFactory));

            return testCommand.DoRun(args);
        }

        public TestCommand(IDotnetTestRunnerFactory testRunnerFactory)
        {
            _dotnetTestRunnerFactory = testRunnerFactory;
        }

        public int DoRun(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var dotnetTestParams = new DotnetTestParams();

            try
            {
                dotnetTestParams.Parse(args);

                if (dotnetTestParams.Help)
                {
                    return 0;
                }

                // Register for parent process's exit event
                if (dotnetTestParams.ParentProcessId.HasValue)
                {
                    RegisterForParentProcessExit(dotnetTestParams.ParentProcessId.Value);
                }

                return RunTest(dotnetTestParams);
            }
            catch (InvalidOperationException ex)
            {
                TestHostTracing.Source.TraceEvent(TraceEventType.Error, 0, ex.ToString());
                return -1;
            }
            catch (Exception ex) when (!(ex is GracefulException))
            {
                TestHostTracing.Source.TraceEvent(TraceEventType.Error, 0, ex.ToString());
                return -2;
            }
        }

        private static void RegisterForParentProcessExit(int id)
        {
            var parentProcess = Process.GetProcesses().FirstOrDefault(p => p.Id == id);

            if (parentProcess != null)
            {
                parentProcess.EnableRaisingEvents = true;
                parentProcess.Exited += (sender, eventArgs) =>
                {
                    TestHostTracing.Source.TraceEvent(
                        TraceEventType.Information,
                        0,
                        "Killing the current process as parent process has exited.");

                    Process.GetCurrentProcess().Kill();
                };
            }
            else
            {
                TestHostTracing.Source.TraceEvent(
                    TraceEventType.Information,
                    0,
                    "Failed to register for parent process's exit event. " +
                    $"Parent process with id '{id}' was not found.");
            }
        }

        private int RunTest(DotnetTestParams dotnetTestParams)
        {
            var dotnetTestRunner = _dotnetTestRunnerFactory.Create(dotnetTestParams);
            return dotnetTestRunner.RunTests(dotnetTestParams);
        }
    }
}