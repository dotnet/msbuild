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

                var projectPath = GetProjectPath(dotnetTestParams.ProjectPath);
                var runtimeIdentifiers = !string.IsNullOrEmpty(dotnetTestParams.Runtime) ?
                    new[] { dotnetTestParams.Runtime } :
                    RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers();
                var exitCode = 0;

                // Create a workspace
                var workspace = new BuildWorkspace(ProjectReaderSettings.ReadFromEnvironment());

                if (dotnetTestParams.Framework != null)
                {
                    var projectContext = workspace.GetProjectContext(projectPath, dotnetTestParams.Framework);
                    if (projectContext == null)
                    {
                        Reporter.Error.WriteLine($"Project '{projectPath}' does not support framework: {dotnetTestParams.UnparsedFramework}");
                        return 1;
                    }
                    projectContext = workspace.GetRuntimeContext(projectContext, runtimeIdentifiers);

                    exitCode = RunTest(projectContext, dotnetTestParams, workspace);
                }
                else
                {
                    var summary = new Summary();
                    var projectContexts = workspace.GetProjectContextCollection(projectPath)
                        .EnsureValid(projectPath)
                        .FrameworkOnlyContexts
                        .Select(c => workspace.GetRuntimeContext(c, runtimeIdentifiers))
                        .ToList();

                    // Execute for all TFMs the project targets.
                    foreach (var projectContext in projectContexts)
                    {
                        var result = RunTest(projectContext, dotnetTestParams, workspace);
                        if (result == 0)
                        {
                            summary.Passed++;
                        }
                        else
                        {
                            summary.Failed++;
                            if (exitCode == 0)
                            {
                                // If tests fail in more than one TFM, we'll have it use the result of the first one
                                // as the exit code.
                                exitCode = result;
                            }
                        }
                    }

                    summary.Print();
                }

                return exitCode;
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

        public static int Run(string[] args)
        {
            var testCommand = new TestCommand(new DotnetTestRunnerFactory());

            return testCommand.DoRun(args);
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

        private int RunTest(ProjectContext projectContext, DotnetTestParams dotnetTestParams, BuildWorkspace workspace)
        {
            var testRunner = projectContext.ProjectFile.TestRunner;
            var dotnetTestRunner = _dotnetTestRunnerFactory.Create(dotnetTestParams.Port);
            return dotnetTestRunner.RunTests(projectContext, dotnetTestParams, workspace);
        }

        private static string GetProjectPath(string projectPath)
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

            return projectPath;
        }

        private class Summary
        {
            public int Passed { get; set; }

            public int Failed { get; set; }

            public int Total => Passed + Failed;

            public void Print()
            {
                var summaryMessage = $"SUMMARY: Total: {Total} targets, Passed: {Passed}, Failed: {Failed}.";
                if (Failed > 0)
                {
                    Reporter.Error.WriteLine(summaryMessage.Red());
                }
                else
                {
                    Reporter.Output.WriteLine(summaryMessage);
                }
            }
        }
    }
}