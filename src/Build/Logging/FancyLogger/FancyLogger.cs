using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

using Microsoft.Build.Framework;
// using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace FancyLogger
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
                ANSIBuilder.Cursor.GoToPosition(lineNumber, 0) +
                line.Text +
                ANSIBuilder.Cursor.GoToPosition(lineNumber+1, 0)
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
                    ANSIBuilder.Cursor.GoToPosition(line.LineNumber, 0)
                    + "\r" + text +
                    ANSIBuilder.Cursor.GoToPosition(line.LineNumber + 1, 0)
                );
            }
        }
        public static void DeleteLine(int lineId)
        {
            return;
        }
    }


    #region ANSI Formatting
    internal enum ANSIColors
    {
        Red = 31,
        Green = 32,
        Yellow = 33,
        Blue = 34,
        Magenta = 35,
        Cyan = 36,
        White = 37
    }
    internal static class ANSIBuilder
    {
        internal static class Formatting
        {
            public static string Bold(string text)
            {
                return String.Format("\x1b[1m{0}\x1b[22m", text);
            }
            public static string Dim(string text)
            {
                return String.Format("\x1b[2m{0}\x1b[22m", text);
            }
            public static string Italic(string text)
            {
                return String.Format("\x1b[3m{0}\x1b[23m", text);
            }
            public static string Underline(string text)
            {
                return String.Format("\x1b[4m{0}\x1b[24m", text);
            }
            public static string Blinking(string text)
            {
                return String.Format("\x1b[5m{0}\x1b[25m", text);
            }
            public static string StrikeThrough(string text)
            {
                return String.Format("\x1b[9m{0}\x1b[29m", text);
            }
            public static string Color(string text, ANSIColors color)
            {
                return String.Format("\x1b[{0}m{1}\x1b[0m", (int) color, text);
            }
        }

        internal static class Cursor
        {
            private static int savedCursorLine = 0;
            public static string GoToPosition(int line, int column)
            {
                return String.Format("\x1b[{0};{1}H", line, column);
                // Console.SetCursorPosition(line, column);
                // return "";
            }
            public static string SaveCursorPosition()
            {
                savedCursorLine = Console.CursorTop;
                return "";
                // return "\x1b 7";
            }
            public static string RestoreCursorPosition()
            {
                return GoToPosition(savedCursorLine, 0);
                // return "\x1b 8";
            }
        }

        internal static class Eraser
        {
            public static string EraseLine()
            {
                return "\x1b[2K";
            }
        }

        internal static class Graphics
        {
            public static string ProgressBar(float percentage, int width = 10, char completedChar= '█', char remainingChar = '░')
            {
                string result = "[";
                for (int i = 0; i < (int) Math.Floor(width * percentage); i++)
                {
                    result += completedChar;
                }
                for (int i = (int) Math.Floor(width * percentage) + 1; i < width; i++)
                {
                    result += remainingChar;
                }
                return result + "]";
            }
        }
    }
    #endregion

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
            // Console.WriteLine(LoggerFormatting.Bold("[Project]") + "\t Started");
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
                    ANSIBuilder.Formatting.Dim("\tTasks will go here")
                );
                // Log.WriteNewLine("");

                // Log.WriteInLine("Task task task task task task", nextLine.Id);
                // Number of 
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
                        ANSIBuilder.Formatting.Color("✓ " + e.TargetName, ANSIColors.Green)
                    , lineId);
                }
                Log.WriteInLine(
                    ANSIBuilder.Eraser.EraseLine(), lineId+1
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
                    ANSIBuilder.Eraser.EraseLine() + "\t" + ANSIBuilder.Graphics.ProgressBar(0.6f, 16) + "\t" +
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
