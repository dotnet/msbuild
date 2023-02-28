// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.LiveLogger
{
    internal class LiveLogger : ILogger
    {
        private ConcurrentDictionary<int, ProjectNode> projects = new();

        private bool succeeded;
        private int startedProjects = 0;
        private int finishedProjects = 0;
        private ConcurrentDictionary<string, int> blockedProjects = new();

        private Stopwatch? _stopwatch;

        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }

        /// <summary>
        /// List of events the logger needs as parameters to the <see cref="ConfigurableForwardingLogger"/>.
        /// </summary>
        /// <remarks>
        /// If LiveLogger runs as a distributed logger, MSBuild out-of-proc nodes might filter the events that will go to the main node using an instance of <see cref="ConfigurableForwardingLogger"/> with the following parameters.
        /// </remarks>
        public static readonly string[] ConfigurableForwardingLoggerParameters =
        {
            "BUILDSTARTEDEVENT",
            "BUILDFINISHEDEVENT",
            "PROJECTSTARTEDEVENT",
            "PROJECTFINISHEDEVENT",
            "TARGETSTARTEDEVENT",
            "TARGETFINISHEDEVENT",
            "TASKSTARTEDEVENT",
            "HIGHMESSAGEEVENT",
            "WARNINGEVENT",
            "ERROREVENT"
        };

        public LiveLogger()
        {
            Parameters = "";
        }

        public void Initialize(IEventSource eventSource)
        {
            // Start the stopwatch as soon as the logger is initialized to capture
            // any time before the BuildStarted event
            _stopwatch = Stopwatch.StartNew();
            // Register for different events. Make sure that ConfigurableForwardingLoggerParameters are in sync with them.
            // Started and Finished events  
            eventSource.BuildStarted += new BuildStartedEventHandler(eventSource_BuildStarted);
            eventSource.BuildFinished += new BuildFinishedEventHandler(eventSource_BuildFinished);
            eventSource.ProjectStarted += new ProjectStartedEventHandler(eventSource_ProjectStarted);
            eventSource.ProjectFinished += new ProjectFinishedEventHandler(eventSource_ProjectFinished);
            eventSource.TargetStarted += new TargetStartedEventHandler(eventSource_TargetStarted);
            eventSource.TargetFinished += new TargetFinishedEventHandler(eventSource_TargetFinished);
            eventSource.TaskStarted += new TaskStartedEventHandler(eventSource_TaskStarted);

            // Messages/Warnings/Errors
            // BuildMessageEventHandler event handler below currently process only High importance events. 
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

        private void Render()
        {
            // Initialize LiveLoggerBuffer
            TerminalBuffer.Initialize();
            // TODO: Fix. First line does not appear at top. Leaving empty line for now
            TerminalBuffer.WriteNewLine(string.Empty);

            // Top line indicates the number of finished projects.
            TerminalBuffer.FinishedProjects = this.finishedProjects;

            // First render
            TerminalBuffer.Render();
            int i = 0;
            // Rerender periodically
            while (!TerminalBuffer.IsTerminated)
            {
                i++;
                // Delay by 1/60 seconds
                // Use task delay to avoid blocking the task, so that keyboard input is listened continously
                Task.Delay((i / 60) * 1_000).ContinueWith((t) =>
                {
                    TerminalBuffer.FinishedProjects = this.finishedProjects;

                    // Rerender projects only when needed
                    foreach (var project in projects)
                    {
                        project.Value.Log();
                    }

                    // Rerender buffer
                    TerminalBuffer.Render();
                });
                // Handle keyboard input
                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey().Key;
                    switch (key)
                    {
                        case ConsoleKey.UpArrow:
                            if (TerminalBuffer.TopLineIndex > 0)
                            {
                                TerminalBuffer.TopLineIndex--;
                            }
                            TerminalBuffer.ShouldRerender = true;
                            break;
                        case ConsoleKey.DownArrow:
                            TerminalBuffer.TopLineIndex++;
                            TerminalBuffer.ShouldRerender = true;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void UpdateFooter()
        {
            float percentage = startedProjects == 0 ? 0.0f : (float)finishedProjects / startedProjects;
            TerminalBuffer.FooterText = ANSIBuilder.Alignment.SpaceBetween(
                $"Build progress (approx.) [{ANSIBuilder.Graphics.ProgressBar(percentage)}]",
                ANSIBuilder.Formatting.Italic(ANSIBuilder.Formatting.Dim("[Up][Down] Scroll")),
                Console.BufferWidth);
        }

        // Build
        private void eventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
        }

        private void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            succeeded = e.Succeeded;
        }

        // Project
        private void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            startedProjects++;

            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;

            // If id does not exist...
            projects.GetOrAdd(id, (_) =>
            {
                // Add project
                ProjectNode node = new(e)
                {
                    ShouldRerender = true,
                };
                UpdateFooter();

                return node;
            });
        }

        private void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out ProjectNode? node))
            {
                return;
            }

            // Update line
            node.Finished = true;
            node.ShouldRerender = true;
            finishedProjects++;
            UpdateFooter();
        }

        // Target
        private void eventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out ProjectNode? node))
            {
                return;
            }
            // Update
            node.AddTarget(e);
            // Log
            node.ShouldRerender = true;
        }

        private void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out ProjectNode? node))
            {
                return;
            }
            // Update
            node.FinishedTargets++;
            // Log
            node.ShouldRerender = true;
        }

        // Task
        private void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out ProjectNode? node))
            {
                return;
            }
            // Update
            node.AddTask(e);
            // Log
            node.ShouldRerender = true;

            if (e.TaskName.Equals("MSBuild"))
            {
                TerminalBufferLine? line = null; // TerminalBuffer.WriteNewLineAfterMidpoint($"{e.ProjectFile} is blocked by the MSBuild task.");
                if (line is not null)
                {
                    blockedProjects[e.ProjectFile] = line.Id;
                }
            }
        }

        // Raised messages, warnings and errors
        private void eventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            if (e is TaskCommandLineEventArgs)
            {
                return;
            }
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out ProjectNode? node))
            {
                return;
            }
            // Update
            node.AddMessage(e);
            // Log
            node.ShouldRerender = true;
        }

        private void eventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out ProjectNode? node))
            {
                return;
            }
            // Update
            node.AddWarning(e);
            // Log
            node.ShouldRerender = true;
        }

        private void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out ProjectNode? node))
            {
                return;
            }
            // Update
            node.AddError(e);
            // Log
            node.ShouldRerender = true;
        }

        private void console_CancelKeyPressed(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            // Shutdown logger
            Shutdown();
        }

        public void Shutdown()
        {
            TerminalBuffer.Terminate();
            int errorCount = 0;
            int warningCount = 0;
            foreach (var project in projects)
            {
                if (project.Value.AdditionalDetails.Count == 0)
                {
                    continue;
                }

                Console.WriteLine(project.Value.ToANSIString());
                errorCount += project.Value.ErrorCount;
                warningCount += project.Value.WarningCount;
                foreach (var message in project.Value.AdditionalDetails)
                {
                    Console.WriteLine($"    └── {message.ToANSIString()}");
                }
                Console.WriteLine();
            }

            // Empty line
            Console.WriteLine();

            Debug.Assert(_stopwatch is not null, $"Expected {nameof(_stopwatch)} to be initialized long before Shutdown()");
            TimeSpan buildDuration = _stopwatch!.Elapsed;

            string prettyDuration = buildDuration.TotalHours > 1.0 ?
                buildDuration.ToString(@"h\:mm\:ss") :
                buildDuration.ToString(@"m\:ss");

            string status = succeeded ?
                ANSIBuilder.Formatting.Color("succeeded", ANSIBuilder.Formatting.ForegroundColor.Green) :
                ANSIBuilder.Formatting.Color("failed", ANSIBuilder.Formatting.ForegroundColor.Red);

            Console.WriteLine($"Build {status} in {prettyDuration}");
            Console.WriteLine($"\t{warningCount} Warnings(s)");
            Console.WriteLine($"\t{errorCount} Errors(s)");
            Console.WriteLine();
        }
    }
}
