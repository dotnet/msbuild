// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.FancyLogger
{ 
    public class FancyLoggerProjectNode
    {
        private static string GetUnambiguousPath(string path)
        {
            return Path.GetFileName(path);
        }

        public int Id;
        public string ProjectPath;
        public bool Finished;
        // Line to display project info
        public FancyLoggerBufferLine? Line;
        // Targets
        public int FinishedTargets;
        public FancyLoggerBufferLine? CurrentTargetLine;
        public FancyLoggerTargetNode? CurrentTargetNode;

        public FancyLoggerProjectNode(ProjectStartedEventArgs args)
        {
            Id = args.ProjectId;
            ProjectPath = args.ProjectFile!;
            Finished = false;
            FinishedTargets = 0;
        }

        public void Log()
        {
            string lineContents = ANSIBuilder.Alignment.SpaceBetween(
                $"{(Finished ? ANSIBuilder.Formatting.Color("✓", ANSIBuilder.Formatting.ForegroundColor.Green) : ANSIBuilder.Graphics.Spinner())} {ANSIBuilder.Formatting.Dim("Project: ")} {ANSIBuilder.Formatting.Color(ANSIBuilder.Formatting.Bold(GetUnambiguousPath(ProjectPath)), Finished ? ANSIBuilder.Formatting.ForegroundColor.Green : ANSIBuilder.Formatting.ForegroundColor.Default )}",
                $"({FinishedTargets} targets completed)",
                Console.WindowWidth
            );
            // Create or update line
            if (Line == null) Line = FancyLoggerBuffer.WriteNewLine(lineContents);
            else FancyLoggerBuffer.UpdateLine(Line.Id, lineContents);
            // If current target
            if (CurrentTargetNode == null) return;
            // Create or update
            if (Finished && CurrentTargetLine != null)
            {
                FancyLoggerBuffer.DeleteLine(CurrentTargetLine.Id);
                return;
            }
            string currentTargetLineContents = $"   └── {CurrentTargetNode.TargetName} : {CurrentTargetNode.CurrentTaskNode?.TaskName ?? String.Empty}";
            if (CurrentTargetLine == null) CurrentTargetLine = FancyLoggerBuffer.WriteNewLineAfter(currentTargetLineContents, Line.Id);
            else FancyLoggerBuffer.UpdateLine(CurrentTargetLine.Id, currentTargetLineContents);
        }

        public void AddTarget(TargetStartedEventArgs args)
        {
            CurrentTargetNode = new FancyLoggerTargetNode(args);
        }
        public void AddTask(TaskStartedEventArgs args)
        {
            // Get target id
            int targetId = args.BuildEventContext!.TargetId;
            if (CurrentTargetNode?.Id == targetId)
            {
                CurrentTargetNode.AddTask(args);
            }
        }
    }

    public class FancyLoggerTargetNode
    {
        public int Id;
        public string TargetName;
        public FancyLoggerTaskNode? CurrentTaskNode;
        public FancyLoggerTargetNode(TargetStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TargetId;
            TargetName = args.TargetName;
        }
        public void AddTask(TaskStartedEventArgs args)
        {
            CurrentTaskNode = new FancyLoggerTaskNode(args);
        }
    }

    public class FancyLoggerTaskNode
    {
        public int Id;
        public string TaskName;
        public FancyLoggerTaskNode(TaskStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TaskId;
            TaskName = args.TaskName;
        }
    }

    public class FancyLoggerWarningNode
    {
    }

    public class FancyLoggerMessageNode
    {
    }

    public class FancyLoggerErrorNode
    {
    }
}
