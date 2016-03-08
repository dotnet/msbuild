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

            var app = new CommandLineApplication(false)
            {
                Name = "dotnet test",
                FullName = ".NET Test Driver",
                Description = "Test Driver for the .NET Platform"
            };

            app.HelpOption("-?|-h|--help");

            var parentProcessIdOption = app.Option("--parentProcessId", "Used by IDEs to specify their process ID. Test will exit if the parent process does.", CommandOptionType.SingleValue);
            var portOption = app.Option("--port", "Used by IDEs to specify a port number to listen for a connection.", CommandOptionType.SingleValue);
            var configurationOption = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to find the binaries to be run", CommandOptionType.SingleValue);
            var projectPath = app.Argument("<PROJECT>", "The project to test, defaults to the current directory. Can be a path to a project.json or a project directory.");

            app.OnExecute(() =>
            {
                try
                {
                    // Register for parent process's exit event
                    if (parentProcessIdOption.HasValue())
                    {
                        int processId;

                        if (!Int32.TryParse(parentProcessIdOption.Value(), out processId))
                        {
                            throw new InvalidOperationException($"Invalid process id '{parentProcessIdOption.Value()}'. Process id must be an integer.");
                        }

                        RegisterForParentProcessExit(processId);
                    }

                    var projectContexts = CreateProjectContexts(projectPath.Value);

                    var projectContext = projectContexts.First();

                    var testRunner = projectContext.ProjectFile.TestRunner;

                    var configuration = configurationOption.Value() ?? Constants.DefaultConfiguration;

                    var outputPath = output.Value();

                    if (portOption.HasValue())
                    {
                        int port;

                        if (!Int32.TryParse(portOption.Value(), out port))
                        {
                            throw new InvalidOperationException($"{portOption.Value()} is not a valid port number.");
                        }

                        return RunDesignTime(port, projectContext, testRunner, configuration, outputPath);
                    }
                    else
                    {
                        return RunConsole(projectContext, app, testRunner, configuration, outputPath);
                    }
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

            });

            return app.Execute(args);
        }

        private static int RunConsole(
            ProjectContext projectContext,
            CommandLineApplication app,
            string testRunner,
            string configuration,
            string outputPath)
        {
            var commandArgs = new List<string> { GetAssemblyUnderTest(projectContext, configuration, outputPath) };
            commandArgs.AddRange(app.RemainingArguments);

            var commandFactory = 
                new ProjectDependenciesCommandFactory(
                    projectContext.TargetFramework, 
                    configuration, 
                    outputPath,
                    projectContext.ProjectDirectory);
            

            return commandFactory.Create(
                    $"dotnet-{GetCommandName(testRunner)}",
                    commandArgs,
                    projectContext.TargetFramework,
                    configuration)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute()
                .ExitCode;
        }

        private static string GetAssemblyUnderTest(ProjectContext projectContext, string configuration, string outputPath)
        {
            var assemblyUnderTest =
                projectContext.GetOutputPaths(configuration, outputPath: outputPath).CompilationFiles.Assembly;

            if (!string.IsNullOrEmpty(outputPath))
            {
                assemblyUnderTest =
                    projectContext.GetOutputPaths(configuration, outputPath: outputPath).RuntimeFiles.Assembly;
            }

            return assemblyUnderTest;
        }

        private static int RunDesignTime(
            int port,
            ProjectContext
            projectContext,
            string testRunner,
            string configuration,
            string outputPath)
        {
            Console.WriteLine("Listening on port {0}", port);

            HandleDesignTimeMessages(projectContext, testRunner, port, configuration, outputPath);

            return 0;
        }

        private static void HandleDesignTimeMessages(
            ProjectContext projectContext,
            string testRunner,
            int port,
            string configuration,
            string outputPath)
        {
            var reportingChannelFactory = new ReportingChannelFactory();
            var adapterChannel = reportingChannelFactory.CreateAdapterChannel(port);

            try
            {
                var assemblyUnderTest = GetAssemblyUnderTest(projectContext, configuration, outputPath);
                var messages = new TestMessagesCollection();
                using (var dotnetTest = new DotnetTest(messages, assemblyUnderTest))
                {
                    var commandFactory = 
                        new ProjectDependenciesCommandFactory(
                            projectContext.TargetFramework, 
                            configuration, 
                            outputPath,
                            projectContext.ProjectDirectory);
                        
                    var testRunnerFactory = new TestRunnerFactory(GetCommandName(testRunner), commandFactory);

                    dotnetTest
                        .AddNonSpecificMessageHandlers(messages, adapterChannel)
                        .AddTestDiscoveryMessageHandlers(adapterChannel, reportingChannelFactory, testRunnerFactory)
                        .AddTestRunMessageHandlers(adapterChannel, reportingChannelFactory, testRunnerFactory)
                        .AddTestRunnnersMessageHandlers(adapterChannel, reportingChannelFactory);

                    dotnetTest.StartListeningTo(adapterChannel);

                    adapterChannel.Accept();

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
            return $"test-{testRunner}";
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