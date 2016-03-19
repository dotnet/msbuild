// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var dotnetTestParams = new DotnetTestParams();

            dotnetTestParams.Parse(args);

            try
            {
                // Register for parent process's exit event
                if (dotnetTestParams.ParentProcessId.HasValue)
                {
                    RegisterForParentProcessExit(dotnetTestParams.ParentProcessId.Value);
                }

                var projectContexts = CreateProjectContexts(dotnetTestParams.ProjectPath);

                var projectContext = projectContexts.First();

                var testRunner = projectContext.ProjectFile.TestRunner;

                IDotnetTestRunner dotnetTestRunner = new ConsoleTestRunner();
                if (dotnetTestParams.Port.HasValue)
                {
                    dotnetTestRunner = new DesignTimeRunner();
                }

                return dotnetTestRunner.RunTests(projectContext, dotnetTestParams);
            }
            catch (InvalidOperationException ex)
            {
                TestHostTracing.Source.TraceEvent(TraceEventType.Error, 0, ex.ToString());
                return -1;
            }
            catch (Exception ex)
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

        private static IEnumerable<ProjectContext> CreateProjectContexts(string projectPath)
        {
            projectPath = projectPath ?? Directory.GetCurrentDirectory();

            if (!projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.Combine(projectPath, Project.FileName);
            }

            if (!File.Exists(projectPath))
            {
                throw new InvalidOperationException($"{projectPath} does not exist.");
            }

            return ProjectContext.CreateContextForEachFramework(projectPath);
        }
    }
}