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
                FancyLoggerBufferLine rootLine = FancyLoggerBuffer.WriteNewLine("");
                root.Line = rootLine;
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
            // Create node
            FancyLoggerNode node = new FancyLoggerNode(id, FancyLoggerNodeType.Project);
            node.Line = new FancyLoggerBufferLine(" " + ANSIBuilder.Formatting.Dim("Project: ") + e.ProjectFile); ;
            root.Add(node);
            node.Write();
        }
        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            if (e.BuildEventContext?.ProjectInstanceId == null) return;
            FancyLoggerNode? node = root.Find($"project-{e.BuildEventContext.ProjectInstanceId}");
            if (node == null) return;
            int lineId = node.Line?.Id ?? -1;
            if(lineId == -1) return;
            FancyLoggerBuffer.UpdateLine(lineId, ""
                + ANSIBuilder.Formatting.Color("✓", ANSIBuilder.Formatting.ForegroundColor.Green)
                + ANSIBuilder.Formatting.Dim("Project: ")
                + ANSIBuilder.Formatting.Color(e.ProjectFile ?? "", ANSIBuilder.Formatting.ForegroundColor.Green)
            );
            node.Collapse();
        }
        // Target
        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId == null) return;
            int id = e.BuildEventContext.TargetId;
            // Create node
            FancyLoggerNode node = new FancyLoggerNode(id, FancyLoggerNodeType.Target);
            node.Line = new FancyLoggerBufferLine("  "
                + ANSIBuilder.Formatting.Dim("Target: ")
                + e.TargetName);
            // Get parent node
            FancyLoggerNode? parentNode = root.Find($"project-{e.BuildEventContext.ProjectInstanceId}");
            if (parentNode == null) return;
            // Add to parent node
            parentNode.Add(node);
            node.Write();
        }
        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId == null) return;
            FancyLoggerNode? node = root.Find($"target-{e.BuildEventContext.TargetId}");
            if (node == null) return;
            int lineId = node.Line?.Id ?? -1;
            if(lineId == -1) return;
            /*FancyLoggerBuffer.UpdateLine(lineId, ""
                + ANSIBuilder.Formatting.Color("✓ ", ANSIBuilder.Formatting.ForegroundColor.Green)
                + ANSIBuilder.Formatting.Dim("Target: ")
                + ANSIBuilder.Formatting.Color(e.TargetName, ANSIBuilder.Formatting.ForegroundColor.Green)
            );
            node.Collapse();*/
            FancyLoggerBuffer.DeleteLine(lineId);
        }

        // Task
        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            existingTasks++;
            if (e.BuildEventContext?.TaskId == null) return;
            int id = e.BuildEventContext.TaskId;
            // Create node
            FancyLoggerNode node = new FancyLoggerNode(id, FancyLoggerNodeType.Task);
            node.Line = new FancyLoggerBufferLine("  " + ANSIBuilder.Formatting.Dim("Task: ") + e.TaskName);
            // Get parent node
            FancyLoggerNode? parentNode = root.Find($"target-{e.BuildEventContext.TargetId}");
            if (parentNode == null) return;
            // Add to parent node
            parentNode.Add(node);
            node.Write();
            // TODO: Remove
            Thread.Sleep(400);
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e)
        {
            completedTasks++;
            if (e.BuildEventContext?.TaskId == null) return;
            FancyLoggerNode? node = root.Find($"task-{e.BuildEventContext.TaskId}");
            if(node == null) return;
            int lineId = node.Line?.Id ?? -1;
            if (lineId == -1) return;
            FancyLoggerBuffer.UpdateLine(lineId, ""
                + ANSIBuilder.Formatting.Color("✓ ", ANSIBuilder.Formatting.ForegroundColor.Green)
                + ANSIBuilder.Formatting.Dim("Task: ")
                + ANSIBuilder.Formatting.Color(e.TaskName, ANSIBuilder.Formatting.ForegroundColor.Green)
            );
            FancyLoggerBuffer.WriteFooter($"Build: {ANSIBuilder.Graphics.ProgressBar(completedTasks/existingTasks)}  {(completedTasks / existingTasks) * 100} \t {completedTasks}/{existingTasks}");
            node.Collapse();
        }

        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            // Only output high importance messages
            // if (e.Importance != MessageImportance.High) return;
            /* if (e.BuildEventContext?.TaskId == null) return;
            int id = e.BuildEventContext.GetHashCode();
            FancyLoggerNode node = new FancyLoggerNode(id, FancyLoggerNodeType.Message);
            node.Line = new FancyLoggerBufferLine("--Message");

            FancyLoggerNode? parentNode = root.Find($"task-{e.BuildEventContext.TaskId}");
            if (parentNode == null) return;

            parentNode.Add(node);
            node.Write(); */
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
