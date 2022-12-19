// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.Build.Logging.FancyLogger
{
    public class FancyLogger : ILogger
    {
        // TODO: Move somewhere else
        public string GetUnambiguousPath(string path)
        {
            // Get last part of path
            return Path.GetFileName(path);
        }

        public FancyLoggerNode root = new FancyLoggerNode(-1, FancyLoggerNodeType.None);

        public Dictionary<int, FancyLoggerBufferLine> projectConsoleLines = new Dictionary<int, FancyLoggerBufferLine>();

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
            // eventSource.TargetStarted += new TargetStartedEventHandler(eventSource_TargetStarted);
            // eventSource.TaskStarted += new TaskStartedEventHandler(eventSource_TaskStarted);
            // Finished
            eventSource.BuildFinished += new BuildFinishedEventHandler(eventSource_BuildFinished);
            eventSource.ProjectFinished += new ProjectFinishedEventHandler(eventSource_ProjectFinished);
            // eventSource.TargetFinished += new TargetFinishedEventHandler(eventSource_TargetFinished);
            // eventSource.TaskFinished += new TaskFinishedEventHandler(eventSource_TaskFinished);
            // Raised
            eventSource.MessageRaised += new BuildMessageEventHandler(eventSource_MessageRaised);
            eventSource.WarningRaised += new BuildWarningEventHandler(eventSource_WarningRaised);
            eventSource.ErrorRaised += new BuildErrorEventHandler(eventSource_ErrorRaised);
            // Initialize FancyLoggerBuffer
            FancyLoggerBuffer.Initialize();
        }

        // Build
        void eventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
        }
        void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            // Console.WriteLine(LoggerFormatting.Bold("[Build]") + "\t Finished");
        }

        // Project
        void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            // If id already exists...
            if (projectConsoleLines.ContainsKey(id)) return;
            // Create line
            FancyLoggerBufferLine line = FancyLoggerBuffer.WriteNewLine(
                ANSIBuilder.Alignment.SpaceBetween(
                    $"{ANSIBuilder.Graphics.Spinner()} {ANSIBuilder.Formatting.Dim("Project - ")} {GetUnambiguousPath(e.ProjectFile!)}",
                    "(5 targets completed)",
                    Console.WindowWidth
                )
            );

            projectConsoleLines.Add(id, line);
        }
        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            // Get project id
            int id = e.BuildEventContext!.ProjectInstanceId;
            if(!projectConsoleLines.TryGetValue(id, out FancyLoggerBufferLine? line)) return;
            // Update line
            FancyLoggerBuffer.UpdateLine(line.Id,
                ANSIBuilder.Alignment.SpaceBetween(
                    $"{ANSIBuilder.Formatting.Color("✓", ANSIBuilder.Formatting.ForegroundColor.Green)} {ANSIBuilder.Formatting.Dim("Project - ")} {ANSIBuilder.Formatting.Color(GetUnambiguousPath(e.ProjectFile!), ANSIBuilder.Formatting.ForegroundColor.Green)}",
                    "(5 targets completed)",
                    Console.WindowWidth
                )
            );
        }
        // Target
        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
        }
        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
        }

        // Task
        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            existingTasks++;
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e)
        {
            completedTasks++;
        }

        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
        }
        void eventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            FancyLoggerBuffer.WriteNewLine("Warning");
        }
        void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            // TODO: Try to redirect to stderr
            FancyLoggerBuffer.WriteNewLine("Error");
        }


        public void Shutdown() {
            // Keep open if autoscroll disabled (the user is reading info)
            while (true)
            {
                if (FancyLoggerBuffer.AutoScrollEnabled) break;
            }
            FancyLoggerBuffer.Terminate();
            Console.WriteLine("Build status, warnings and errors will be shown here after the build has ended and the interactive logger has closed");
        }
    }
}
