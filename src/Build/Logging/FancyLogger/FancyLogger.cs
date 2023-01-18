// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.FancyLogger
{
    public class FancyLogger : ILogger
    {   
        private Dictionary<int, FancyLoggerProjectNode> projects = new Dictionary<int, FancyLoggerProjectNode>();
        private bool Succeeded;
        public string Parameters {  get; set; }
        public float StartedProjects = 0;
        public float FinishedProjects = 0;
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
            // Initialize FancyLoggerBuffer
            FancyLoggerBuffer.Initialize();
        }

        // Build
        void eventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
        }
        void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            Succeeded = e.Succeeded;
            // Console.WriteLine(LoggerFormatting.Bold("[Build]") + "\t Finished");
        }

        // Project
        void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            StartedProjects++;
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            // If id already exists...
            if (projects.ContainsKey(id)) return;
            // Add project
            FancyLoggerProjectNode node = new FancyLoggerProjectNode(e);
            projects[id] = node;
            // Log
            node.Log();
            // Update footer
            if (StartedProjects > 0)
            {
                FancyLoggerBuffer.FooterText = ANSIBuilder.Alignment.SpaceBetween(
                    $"Finished projects: {ANSIBuilder.Graphics.ProgressBar(FinishedProjects/StartedProjects)} {FinishedProjects}/{StartedProjects}",
                    ANSIBuilder.Formatting.Italic(ANSIBuilder.Formatting.Dim("[Up][Down] Scroll")),
                    Console.BufferWidth
                );
            }
        }
        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node)) return;
            // Update line
            node.Finished = true;
            node.Log();
            // Update footer
            FinishedProjects++;
            if (StartedProjects > 0)
            {
                FancyLoggerBuffer.FooterText = ANSIBuilder.Alignment.SpaceBetween(
                    $"Finished projects: {ANSIBuilder.Graphics.ProgressBar(FinishedProjects / StartedProjects)} {FinishedProjects}/{StartedProjects}",
                    ANSIBuilder.Formatting.Italic(ANSIBuilder.Formatting.Dim("[Up][Down] Scroll")),
                    Console.BufferWidth
                );
            }
        }
        // Target
        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node)) return;
            // Update
            node.AddTarget(e);
            node.Log();
        }
        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node)) return;
            // Update
            node.FinishedTargets++;
            node.Log();
        }

        // Task
        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;

            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node)) return;
            // Update
            node.AddTask(e);
            node.Log();
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e)
        {
        }

        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node)) return;
            // Update
            node.AddMessage(e);
            node.Log();
        }
        void eventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node)) return;
            // Update
            node.AddWarning(e);
            node.Log();
        }
        void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (!projects.TryGetValue(id, out FancyLoggerProjectNode? node)) return;
            // Update
            node.AddError(e);
            node.Log();
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
            Console.Out.Flush();
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
