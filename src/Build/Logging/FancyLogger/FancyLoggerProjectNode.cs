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
    internal class FancyLoggerProjectNode
    {
        /// <summary>
        /// Given a list of paths, this method will get the shortest not ambiguous path for a project.
        /// Example: for `/users/documents/foo/project.csproj` and `/users/documents/bar/project.csproj`, the respective non ambiguous paths would be `foo/project.csproj` and `bar/project.csproj`
        /// Still work in progress...
        /// </summary>
        private static string GetUnambiguousPath(string path)
        {
            return Path.GetFileName(path);
        }

        public int Id;
        public string ProjectPath;
        public string TargetFramework;
        public bool Finished;
        // Line to display project info
        public FancyLoggerBufferLine? Line;
        // Targets
        public int FinishedTargets;
        public FancyLoggerBufferLine? CurrentTargetLine;
        public FancyLoggerTargetNode? CurrentTargetNode;
        // Messages, errors and warnings
        List<FancyLoggerMessageNode> AdditionalDetails = new();
        public FancyLoggerProjectNode(ProjectStartedEventArgs args)
        {
            Id = args.ProjectId;
            ProjectPath = args.ProjectFile!;
            Finished = false;
            FinishedTargets = 0;
            if (args.GlobalProperties != null && args.GlobalProperties.ContainsKey("TargetFramework"))
            {
                TargetFramework = args.GlobalProperties["TargetFramework"];
            }
            else
            {
                TargetFramework = "";
            }
        }

        public void Log()
        {
            // Project details
            string lineContents = ANSIBuilder.Alignment.SpaceBetween(
                $"{(Finished ? ANSIBuilder.Formatting.Color("✓", ANSIBuilder.Formatting.ForegroundColor.Green) : ANSIBuilder.Graphics.Spinner())} {ANSIBuilder.Formatting.Dim("Project: ")} {ANSIBuilder.Formatting.Color(ANSIBuilder.Formatting.Bold(GetUnambiguousPath(ProjectPath)), Finished ? ANSIBuilder.Formatting.ForegroundColor.Green : ANSIBuilder.Formatting.ForegroundColor.Default )} [{TargetFramework}]",
                $"({FinishedTargets} targets completed)",
                Console.WindowWidth
            );

            // Create or update line
            if (Line == null) Line = FancyLoggerBuffer.WriteNewLine(lineContents);
            else FancyLoggerBuffer.UpdateLine(Line.Id, lineContents);

            // For finished projects
            if (Finished)
            {
                if (CurrentTargetLine != null) FancyLoggerBuffer.DeleteLine(CurrentTargetLine.Id);
                foreach (FancyLoggerMessageNode node in AdditionalDetails.ToList())
                {
                    // Only delete high priority messages
                    if (node.Type != FancyLoggerMessageNode.MessageType.HighPriorityMessage) continue;
                    if (node.Line != null) FancyLoggerBuffer.DeleteLine(node.Line.Id);
                    AdditionalDetails.Remove(node);
                }
            }

            // Current target details
            if (CurrentTargetNode == null) return;
            string currentTargetLineContents = $"    └── {CurrentTargetNode.TargetName} : {CurrentTargetNode.CurrentTaskNode?.TaskName ?? String.Empty}";
            if (CurrentTargetLine == null) CurrentTargetLine = FancyLoggerBuffer.WriteNewLineAfter(Line!.Id, currentTargetLineContents);
            else FancyLoggerBuffer.UpdateLine(CurrentTargetLine.Id, currentTargetLineContents);

            // Messages, warnings and errors
            foreach (FancyLoggerMessageNode node in AdditionalDetails)
            {
                if (node.Line == null) node.Line = FancyLoggerBuffer.WriteNewLineAfter(Line!.Id, "Message");
                node.Log();
            }
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
        public void AddMessage(BuildMessageEventArgs args)
        {
            if (args.Importance != MessageImportance.High) return;
            AdditionalDetails.Add(new FancyLoggerMessageNode(args));
        }
        public void AddWarning(BuildWarningEventArgs args)
        {
            AdditionalDetails.Add(new FancyLoggerMessageNode(args));
        }
        public void AddError(BuildErrorEventArgs args)
        {
            AdditionalDetails.Add(new FancyLoggerMessageNode(args));
        }
    }
}
