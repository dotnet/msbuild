using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Logging;

namespace Microsoft.Build.Logging.FancyLogger
{
    public class LogLine
    {
        private static int IdCounter = 0;
        public int Id { get; private set; }
        public string Text;
        public LogLine(string text)
        {
            Id = IdCounter++;
            Text = text;
        }
        public int LineNumber
        {
            get
            {
                return Array.IndexOf( Log.LogLines.Keys.ToArray(), Id ) + Log.InitialCursorHeight + 1;
            }
        }
    }

    public static class Log
    {
        public static int InitialCursorHeight;
        public static Dictionary<int, LogLine> LogLines = new Dictionary<int, LogLine>();

        public static LogLine WriteNewLine(string text)
        {
            // Get starting cursor position
            int lineNumber = LogLines.Count + InitialCursorHeight + 1;
            // Create, add and print line
            LogLine line = new LogLine(text);
            LogLines.Add(line.Id, line);
            Console.Write(
                "\n" +
                // ANSIBuilder.Cursor.GoToPosition(lineNumber, 0) +
                line.Text
                // ANSIBuilder.Cursor.GoToPosition(lineNumber+1, 0)
            );
            // Return line
            return line;
        }
        public static void WriteInLine(string text, int lineId)
        {
            // Get Line id
            LogLine line = Log.LogLines[lineId];
            if(line != null)
            {
                // Replace text on line
                line.Text = text;
                // Log it
                Console.Write(
                    // ANSIBuilder.Cursor.GoToPosition(line.LineNumber, 0)
                    "\r" + text
                    // ANSIBuilder.Cursor.GoToPosition(line.LineNumber + 1, 0)
                );
            }
        }
        public static void DeleteLine(int lineId)
        {
            return;
        }
    }

    public class FancyLogger : ILogger
    {
        public int i = 0;
        public Dictionary<int, int> projectConsoleLines = new Dictionary<int, int>();
        public Dictionary<int, int> tasksConsoleLines = new Dictionary<int, int>();
        public Dictionary<int, int> targetConsoleLines = new Dictionary<int, int>();

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

            // TODO: Review values
            // Console.BufferHeight = Int16.MaxValue - 10;
            Log.InitialCursorHeight = Console.CursorTop;

            Log.WriteNewLine(
                "MSBuild Fancy Console Logger"
            );
        }

        // Build
        void eventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
            // Console.WriteLine( LoggerFormatting.Bold("[Build]") + "\t Started");
        }
        void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            // Console.WriteLine(LoggerFormatting.Bold("[Build]") + "\t Finished");
        }

        // Project
        void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            Log.WriteNewLine(
                ANSIBuilder.Formatting.Color(
                    ANSIBuilder.Formatting.Bold(String.Format("Project {0} started", e.ProjectFile)), ANSIForegroundColor.Yellow
                )
            );
        }
        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            // Console.WriteLine(LoggerFormatting.Bold("[Project]") + "\t Finished");
        }

        // Target
        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId != null)
            {
                LogLine line = Log.WriteNewLine(
                    "  " + e.TargetName 
                );
                targetConsoleLines[e.BuildEventContext.TargetId] = line.Id;

                LogLine nextLine = Log.WriteNewLine(
                    ANSIBuilder.Formatting.Dim("\t~~~") 
                );
                Log.WriteNewLine("");
            }
        }
        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId != null)
            {
                int lineId = targetConsoleLines[e.BuildEventContext.TargetId];
                // If succeeded
                if (e.Succeeded)
                {
                    Log.WriteInLine(
                        ANSIBuilder.Formatting.Color("✓ " + e.TargetName, ANSIForegroundColor.Green)
                    , lineId);
                }
                Log.WriteInLine(
                    ANSIBuilder.Eraser.EraseCurrentLine(), lineId+1
                );
            }
        }

        // Task
        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            if (e.BuildEventContext?.TargetId != null)
            {
                int targetLineId = targetConsoleLines[e.BuildEventContext.TargetId];
                Log.WriteInLine(
                    ANSIBuilder.Eraser.EraseCurrentLine() + "\t" +
                    ANSIBuilder.Graphics.ProgressBar(0.6f, 16) + "\t" +
                    ANSIBuilder.Formatting.Dim(e.TaskName), 
                    targetLineId + 1
                );
                System.Threading.Thread.Sleep(100);
            }
            // Console.WriteLine("\tA task has started");
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


        public void Shutdown() { }
    }
}
