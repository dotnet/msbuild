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

        public FancyLoggerNode root = new FancyLoggerNode(-1, FancyLoggerNodeType.None);

        public Dictionary<int, FancyLoggerBufferLine> projectConsoleLines = new Dictionary<int, FancyLoggerBufferLine>();
        public Dictionary<int, FancyLoggerBufferLine> targetConsoleLines = new Dictionary<int, FancyLoggerBufferLine>();
        public Dictionary<int, FancyLoggerBufferLine> taskConsoleLines = new Dictionary<int, FancyLoggerBufferLine>();

        private float existingTasks = 1;
        private float completedTasks = 1;

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
            int id = e.BuildEventContext.ProjectInstanceId;
            FancyLoggerBufferLine line = FancyLoggerBuffer.WriteNewLine(" "
                + ANSIBuilder.Formatting.Dim("Project: ")
                + e.ProjectFile
            );
            // projectConsoleLines[id] = line;
            // Node
            FancyLoggerNode node = new FancyLoggerNode(id, FancyLoggerNodeType.Project);
            node.Line = line;
            // If has parent project
            /* if (e.ParentProjectBuildEventContext?.ProjectInstanceId != null)
            {
                FancyLoggerNode? parentNode = root.Find($"project-{e.ParentProjectBuildEventContext.ProjectInstanceId}");
                if (parentNode == null) return;
                parentNode.Add(node);
            } else */
            {
                root.Add(node);
            }
        }
        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            if (e.BuildEventContext?.ProjectInstanceId == null) return;
            int lineId = root.Find($"project-{e.BuildEventContext.ProjectInstanceId}")?.Line?.Id ?? -1;
            if(lineId == -1) return;
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
            int id = e.BuildEventContext.TargetId;
            FancyLoggerBufferLine line = FancyLoggerBuffer.WriteNewLine("\t  "
                + ANSIBuilder.Formatting.Dim("Target: ")
                + e.TargetName);
            // Node
            FancyLoggerNode node = new FancyLoggerNode(id, FancyLoggerNodeType.Target);
            node.Line = line;
            FancyLoggerNode? parentNode = root.Find($"project-{e.BuildEventContext.ProjectInstanceId}");
            if (parentNode == null) return;
            parentNode.Add(node);
        }
        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId == null) return;
            int lineId = root.Find($"target-{e.BuildEventContext.TargetId}")?.Line?.Id ?? -1;
            if(lineId == -1) return;
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
            int id = e.BuildEventContext.TaskId;
            FancyLoggerBufferLine line = FancyLoggerBuffer.WriteNewLine("\t\t  "
                + ANSIBuilder.Formatting.Dim("Task: ")
                + e.TaskName
            );
            // Node
            FancyLoggerNode node = new FancyLoggerNode(id, FancyLoggerNodeType.Task);
            node.Line = line;
            FancyLoggerNode? parentNode = root.Find($"target-{e.BuildEventContext.TargetId}");
            if (parentNode == null) return;
            parentNode.Add(node);
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e)
        {
            completedTasks++;
            if (e.BuildEventContext?.TaskId == null) return;
            // int lineId = taskConsoleLines[e.BuildEventContext.TaskId].Id;
            int lineId = root.Find($"task-{e.BuildEventContext.TaskId}")?.Line?.Id ?? -1;
            if (lineId == -1) return;
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
            FancyLoggerBuffer.WriteNewLine("Warning!");
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
