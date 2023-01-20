// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        public string ProjectOutputExecutable;
        // Line to display project info
        public FancyLoggerBufferLine? Line;
        // Targets
        public int FinishedTargets;
        public FancyLoggerBufferLine? CurrentTargetLine;
        public FancyLoggerTargetNode? CurrentTargetNode;
        // Messages, errors and warnings
        public List<FancyLoggerMessageNode> AdditionalDetails = new();
        // Count messages, warnings and errors
        public int MessageCount = 0;
        public int WarningCount = 0;
        public int ErrorCount = 0;
        public FancyLoggerProjectNode(ProjectStartedEventArgs args)
        {
            Id = args.ProjectId;
            ProjectPath = args.ProjectFile!;
            Finished = false;
            FinishedTargets = 0;
            ProjectOutputExecutable = string.Empty;
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
                // Show indicator
                (Finished ? ANSIBuilder.Formatting.Color("✓", ANSIBuilder.Formatting.ForegroundColor.Green) : ANSIBuilder.Formatting.Blinking(ANSIBuilder.Graphics.Spinner())) +
                // Project file path with color
                $" {ANSIBuilder.Formatting.Color(ANSIBuilder.Formatting.Bold(GetUnambiguousPath(ProjectPath)), Finished ? ANSIBuilder.Formatting.ForegroundColor.Green : ANSIBuilder.Formatting.ForegroundColor.Default )}" +
                // TFM
                $" {ANSIBuilder.Formatting.Inverse(TargetFramework)} " +
                (ProjectOutputExecutable.Length > 0 ? $"-> { ANSIBuilder.Formatting.Hyperlink(GetUnambiguousPath(ProjectOutputExecutable), ProjectOutputExecutable) }" : string.Empty)
                ,
                $"({MessageCount} ℹ️, {WarningCount} ⚠️, {ErrorCount} ❌)",
                // ProjectOutputExecutable, 
                Console.WindowWidth
            );;

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
                    // AdditionalDetails.Remove(node);
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
                if (Finished && node.Type == FancyLoggerMessageNode.MessageType.HighPriorityMessage) continue;
                if (node.Line == null) node.Line = FancyLoggerBuffer.WriteNewLineAfter(Line!.Id, "Message");
                node.Log();
            }
        }

        public FancyLoggerTargetNode AddTarget(TargetStartedEventArgs args)
        {
            CurrentTargetNode = new FancyLoggerTargetNode(args);
            return CurrentTargetNode;
        }
        public FancyLoggerTaskNode? AddTask(TaskStartedEventArgs args)
        {
            // Get target id
            int targetId = args.BuildEventContext!.TargetId;
            if (CurrentTargetNode?.Id == targetId) return CurrentTargetNode.AddTask(args);
            else return null;
        }
        public FancyLoggerMessageNode? AddMessage(BuildMessageEventArgs args)
        {
            if (args.Importance != MessageImportance.High) return null;
            MessageCount++;
            // Detect output messages using regex
            // var match = Regex.Match(args.Message, $"(?<={args.ProjectFile} -> )(.*)");
            var match = Regex.Match(args.Message!, $"(?<=.* -> )(.*)");
            if (match.Success)
                ProjectOutputExecutable = match.Value;

            FancyLoggerMessageNode node = new FancyLoggerMessageNode(args);
            AdditionalDetails.Add(node);
            return node;
        }
        public FancyLoggerMessageNode? AddWarning(BuildWarningEventArgs args)
        {
            WarningCount++;
            FancyLoggerMessageNode node = new FancyLoggerMessageNode(args);
            AdditionalDetails.Add(node);
            return node;
        }
        public FancyLoggerMessageNode? AddError(BuildErrorEventArgs args)
        {
            ErrorCount++;
            FancyLoggerMessageNode node = new FancyLoggerMessageNode(args);
            AdditionalDetails.Add(node);
            return node;
        }
    }
}
