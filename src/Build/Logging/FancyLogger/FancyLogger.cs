// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.FancyLogger
{
    public class FancyLogger : ILogger
    {   
        private Dictionary<int, FancyLoggerProjectNode> projects = new Dictionary<int, FancyLoggerProjectNode>();

        private bool Succeeded;

        private float existingTasks = 1;
        private float completedTasks = 0;

        public string Parameters {  get; set; }

        public LoggerVerbosity Verbosity { get; set; }

        public FancyLogger()
        {
            Parameters = "";
        }

        public void Initialize(IEventSource eventSource)
        {
            // Register for different events
            // Started
            eventSource.BuildStarted += new BuildStartedEventHandler(eventSource_BuildStarted);
            eventSource.ProjectStarted += new ProjectStartedEventHandler(eventSource_ProjectStarted);
            eventSource.TargetStarted += new TargetStartedEventHandler(eventSource_TargetStarted);
            eventSource.TaskStarted += new TaskStartedEventHandler(eventSource_TaskStarted);
            // Finished
            eventSource.BuildFinished += new BuildFinishedEventHandler(eventSource_BuildFinished);
            eventSource.ProjectFinished += new ProjectFinishedEventHandler(eventSource_ProjectFinished);
            eventSource.TargetFinished += new TargetFinishedEventHandler(eventSource_TargetFinished);
            // eventSource.TaskFinished += new TaskFinishedEventHandler(eventSource_TaskFinished);
            // Raised
            eventSource.MessageRaised += new BuildMessageEventHandler(eventSource_MessageRaised);
            eventSource.WarningRaised += new BuildWarningEventHandler(eventSource_WarningRaised);
            eventSource.ErrorRaised += new BuildErrorEventHandler(eventSource_ErrorRaised);
            // Cancelled
            Console.CancelKeyPress += new ConsoleCancelEventHandler(console_CancelKeyPressed);
            
            Task.Run(() =>
            {
                Render();
            });
        }

        void Render()
        {
            // Initialize FancyLoggerBuffer
            FancyLoggerBuffer.Initialize();
            // TODO: Fix. First line does not appear at top. Leaving empty line for now
            FancyLoggerBuffer.WriteNewLine(string.Empty);
            // First render
            FancyLoggerBuffer.Render();
            int i = 0;
            // Rerender periodically
            while (!FancyLoggerBuffer.IsTerminated)
            {
                i++;
                // Delay by 1/60 seconds
                // Use task delay to avoid blocking the task, so that keyboard input is listened continously
                Task.Delay((i / 60) * 1_000).ContinueWith((t) =>
                {
                    // Rerender projects only when needed
                    foreach (var project in projects)
                    {
                        project.Value.Log();
                    }
                    // Rerender buffer
                    FancyLoggerBuffer.Render();
                });
                // Handle keyboard input
                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey().Key;
                    switch (key)
                    {
                        case ConsoleKey.UpArrow:
                            if (FancyLoggerBuffer.TopLineIndex > 0)
                            {
                                FancyLoggerBuffer.TopLineIndex--;
                            }
                            FancyLoggerBuffer.ShouldRerender = true;
                            break;
                        case ConsoleKey.DownArrow:
                            FancyLoggerBuffer.TopLineIndex++;
                            FancyLoggerBuffer.ShouldRerender = true;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        // Build
        void eventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
        }

        void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            Succeeded = e.Succeeded;
        }

        // Project
        void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            // If id already exists...
            if (projects.ContainsKey(id))
            {
                return;
            }
            // Add project
            FancyLoggerProjectNode node = new FancyLoggerProjectNode(e);
            projects[id] = node;
            // Log
            node.ShouldRerender = true;
        }

        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node))
            {
                return;
            }
            // Update line
            node.Finished = true;
            // Log
            node.ShouldRerender = true;
        }

        // Target
        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node))
            {
                return;
            }
            // Update
            node.AddTarget(e);
            // Log
            node.ShouldRerender = true;
        }

        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node))
            {
                return;
            }
            // Update
            node.FinishedTargets++;
            // Log
            node.ShouldRerender = true;
        }

        // Task
        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node))
            {
                return;
            }
            // Update
            node.AddTask(e);
            existingTasks++;
            // Log
            node.ShouldRerender = true;
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e)
        {
            completedTasks++;
        }

        // Raised messages, warnings and errors
        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            if (e is TaskCommandLineEventArgs)
            {
                return;
            }
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node))
            {
                return;
            }
            // Update
            node.AddMessage(e);
            // Log
            node.ShouldRerender = true;
        }

        void eventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node))
            {
                return;
            }
            // Update
            node.AddWarning(e);
            // Log
            node.ShouldRerender = true;
        }
        void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node))
            {
                return;
            }
            // Update
            node.AddError(e);
            // Log
            node.ShouldRerender = true;
        }

        void console_CancelKeyPressed(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            // Shutdown logger
            Shutdown();
        }

        public void Shutdown()
        {
            FancyLoggerBuffer.Terminate();
            // TODO: Remove. There is a bug that causes switching to main buffer without deleting the contents of the alternate buffer
            Console.Clear();
            int errorCount = 0;
            int warningCount = 0;
            foreach (var project in projects)
            {
                errorCount += project.Value.ErrorCount;
                warningCount += project.Value.WarningCount;
                foreach (var message in project.Value.AdditionalDetails)
                {
                    Console.WriteLine(message.ToANSIString());
                }
            }

            // Emmpty line
            Console.WriteLine();
            if (Succeeded)
            {
                Console.WriteLine(ANSIBuilder.Formatting.Color("Build succeeded.", ANSIBuilder.Formatting.ForegroundColor.Green));
                Console.WriteLine($"\t{warningCount} Warning(s)");
                Console.WriteLine($"\t{errorCount} Error(s)");
            }
            else
            {
                Console.WriteLine(ANSIBuilder.Formatting.Color("Build failed.", ANSIBuilder.Formatting.ForegroundColor.Red));
                Console.WriteLine($"\t{warningCount} Warnings(s)");
                Console.WriteLine($"\t{errorCount} Errors(s)");
            }
        }
    }
}
