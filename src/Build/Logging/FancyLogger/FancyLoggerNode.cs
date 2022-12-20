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
        public FancyLoggerBufferLine? Line;
        public FancyLoggerBufferLine? CurrentTargetLine;
        public FancyLoggerProjectNode(ProjectStartedEventArgs args)
        {
            Id = args.ProjectId;
            ProjectPath = args.ProjectFile!;
        }

        public void UpdateLine()
        {
            if (Line == null) return;
            FancyLoggerBuffer.UpdateLine(Line.Id,
                ANSIBuilder.Alignment.SpaceBetween(
                    $"{ANSIBuilder.Graphics.Spinner()} {ANSIBuilder.Formatting.Dim("Project - ")} {GetUnambiguousPath(ProjectPath)}",
                    $"({ANSIBuilder.Formatting.Italic("n")} targets completed)",
                    Console.WindowWidth
                )
            );
        }

        public void WriteStart()
        {
            Line = FancyLoggerBuffer.WriteNewLine("");
            CurrentTargetLine = FancyLoggerBuffer.WriteNewLine("   `- Target and task information will be shown here...");
            UpdateLine();
        }
        public void WriteEnd()
        {
            if (Line == null) return;
            FancyLoggerBuffer.UpdateLine(Line.Id,
                ANSIBuilder.Alignment.SpaceBetween(
                    $"{ANSIBuilder.Formatting.Color("✓", ANSIBuilder.Formatting.ForegroundColor.Green)} {ANSIBuilder.Formatting.Dim("Project - ")} {ANSIBuilder.Formatting.Color(GetUnambiguousPath(ProjectPath), ANSIBuilder.Formatting.ForegroundColor.Green)}",
                    "(All targets complete)",
                    Console.WindowWidth
                )
            );
        }

        public void WriteTarget(TargetStartedEventArgs args)
        {
            if (Line == null) return;
            // Update spinner
            UpdateLine();
            // Create target node

            /* FancyLoggerTargetNode targetNode = new FancyLoggerTargetNode(args);
            FancyLoggerBuffer.WriteNewLineAfter(
                $"-- Target {targetNode.TargetName}",
                Line.Id
            ); */
        }
    }

    public class FancyLoggerTargetNode
    {
        public int Id;
        public string TargetName;
        public FancyLoggerTargetNode(TargetStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TargetId;
            TargetName = args.TargetName;
        }
    }
}
