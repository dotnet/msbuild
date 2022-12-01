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
        public Dictionary<int, int> projectConsoleLines = new Dictionary<int, int>();
        public Dictionary<int, int> tasksConsoleLines = new Dictionary<int, int>();
        public Dictionary<int, FancyLoggerBufferLine> targetConsoleLines = new Dictionary<int, FancyLoggerBufferLine>();

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
        }
        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
        }
        // Target
        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId == null) return;
            targetConsoleLines[e.BuildEventContext.TargetId] = FancyLoggerBuffer.WriteNewLine("  "
                + ANSIBuilder.Formatting.Dim("Target: ")
                + e.TargetName);
            Thread.Sleep(500);
        }
        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId == null) return;
            int lineId = targetConsoleLines[e.BuildEventContext.TargetId].Id;
            FancyLoggerBuffer.UpdateLine(lineId, ""
                + ANSIBuilder.Formatting.Color("✓", ANSIBuilder.Formatting.ForegroundColor.Green)
                + ANSIBuilder.Formatting.Dim("Target: ")
                + ANSIBuilder.Formatting.Color(e.TargetName, ANSIBuilder.Formatting.ForegroundColor.Green)
            );
        }

        // Task
        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e)
        {
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
            Console.Write(ANSIBuilder.Buffer.UseMainBuffer());
            Console.WriteLine("Build status, warnings and errors will be shown here after the build has ended and the interactive logger has closed");
        }
    }
}
