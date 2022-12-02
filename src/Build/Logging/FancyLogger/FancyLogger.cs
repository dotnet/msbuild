using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging.FancyLogger
{
    public class FancyLogger : ILogger
    {
        public Dictionary<int, FancyLoggerBufferLine> projectConsoleLines = new Dictionary<int, FancyLoggerBufferLine>();
        public Dictionary<int, FancyLoggerBufferLine> targetConsoleLines = new Dictionary<int, FancyLoggerBufferLine>();
        public Dictionary<int, FancyLoggerBufferLine> taskConsoleLines = new Dictionary<int, FancyLoggerBufferLine>();

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
            eventSource.TaskFinished += new TaskFinishedEventHandler(eventSource_TaskFinished);
            // Raised
            eventSource.MessageRaised += new BuildMessageEventHandler(eventSource_MessageRaised);
            eventSource.WarningRaised += new BuildWarningEventHandler(eventSource_WarningRaised);
            eventSource.ErrorRaised += new BuildErrorEventHandler(eventSource_ErrorRaised);
            {
                FancyLoggerBuffer.Initialize();
            }
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
            if (e.BuildEventContext?.ProjectInstanceId == null) return;
            projectConsoleLines[e.BuildEventContext.ProjectInstanceId] = FancyLoggerBuffer.WriteNewLine(" "
                + ANSIBuilder.Formatting.Dim("Project: ")
                + e.ProjectFile
            );
        }
        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            if (e.BuildEventContext?.ProjectInstanceId == null) return;
            int lineId = projectConsoleLines[e.BuildEventContext.ProjectInstanceId].Id;
            FancyLoggerBuffer.UpdateLine(lineId, ""
                + ANSIBuilder.Formatting.Color("✓", ANSIBuilder.Formatting.ForegroundColor.Green)
                + ANSIBuilder.Formatting.Dim("Project: ")
                + ANSIBuilder.Formatting.Color(e.ProjectFile ?? "", ANSIBuilder.Formatting.ForegroundColor.Green)
            );
        }
        // Target
        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId == null) return;
            targetConsoleLines[e.BuildEventContext.TargetId] = FancyLoggerBuffer.WriteNewLine("\t  "
                + ANSIBuilder.Formatting.Dim("Target: ")
                + e.TargetName);
        }
        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId == null) return;
            int lineId = targetConsoleLines[e.BuildEventContext.TargetId].Id;
            FancyLoggerBuffer.UpdateLine(lineId, "\t"
                + ANSIBuilder.Formatting.Color("✓ ", ANSIBuilder.Formatting.ForegroundColor.Green)
                + ANSIBuilder.Formatting.Dim("Target: ")
                + ANSIBuilder.Formatting.Color(e.TargetName, ANSIBuilder.Formatting.ForegroundColor.Green)
            );
        }

        // Task
        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            existingTasks++;
            if (e.BuildEventContext?.TaskId == null) return;
            taskConsoleLines[e.BuildEventContext.TaskId] = FancyLoggerBuffer.WriteNewLine("\t\t  "
                + ANSIBuilder.Formatting.Dim("Task: ")
                + e.TaskName
            );
            FancyLoggerBuffer.WriteFooter($"Build: {(completedTasks / existingTasks) * 100}");
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e)
        {
            completedTasks++;
            if (e.BuildEventContext?.TaskId == null) return;
            int lineId = taskConsoleLines[e.BuildEventContext.TaskId].Id;
            FancyLoggerBuffer.UpdateLine(lineId, "\t\t"
                + ANSIBuilder.Formatting.Color("✓ ", ANSIBuilder.Formatting.ForegroundColor.Green)
                + ANSIBuilder.Formatting.Dim("Task: ")
                + ANSIBuilder.Formatting.Color(e.TaskName, ANSIBuilder.Formatting.ForegroundColor.Green)
            );
            FancyLoggerBuffer.WriteFooter($"Build: {(completedTasks / existingTasks) * 100}");
        }

        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            // Message raised
        }
        void eventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            // Console.WriteLine("Warning raised");
        }
        void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            // TODO: Try to redirect to stderr
            // Console.WriteLine("Error raised");
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
