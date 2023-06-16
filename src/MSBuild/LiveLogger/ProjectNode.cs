// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.LiveLogger
{
    internal class ProjectNode
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
        public string? ProjectOutputExecutable;
        // Line to display project info
        public TerminalBufferLine? Line;
        // Targets
        public int FinishedTargets;
        public TerminalBufferLine? CurrentTargetLine;
        public TargetNode? CurrentTargetNode;
        // Messages, errors and warnings
        public List<MessageNode> AdditionalDetails = new();
        // Count messages, warnings and errors
        public int MessageCount = 0;
        public int WarningCount = 0;
        public int ErrorCount = 0;
        // Bool if node should rerender
        internal bool ShouldRerender = true;
        public ProjectNode(ProjectStartedEventArgs args)
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

        public string ToANSIString()
        {
            ANSIBuilder.Formatting.ForegroundColor color = ANSIBuilder.Formatting.ForegroundColor.Default;
            string icon = ANSIBuilder.Graphics.Spinner() + " ";

            if (Finished && WarningCount + ErrorCount == 0)
            {
                color = ANSIBuilder.Formatting.ForegroundColor.Green;
                icon = "✓";
            }
            else if (ErrorCount > 0)
            {
                color = ANSIBuilder.Formatting.ForegroundColor.Red;
                icon = "X";
            }
            else if (WarningCount > 0)
            {
                color = ANSIBuilder.Formatting.ForegroundColor.Yellow;
                icon = "✓";
            }
            return icon + " " + ANSIBuilder.Formatting.Color(ANSIBuilder.Formatting.Bold(GetUnambiguousPath(ProjectPath)), color) + " " + ANSIBuilder.Formatting.Inverse(TargetFramework);
        }

        // TODO: Rename to Render() after LiveLogger's API becomes internal
        public void Log()
        {
            if (!ShouldRerender)
            {
                return;
            }

            ShouldRerender = false;
            // Project details
            string lineContents = ANSIBuilder.Alignment.SpaceBetween(ToANSIString(), $"({MessageCount} ℹ️, {WarningCount} ⚠️, {ErrorCount} ❌)", Console.BufferWidth - 1);
            // Create or update line
            if (Line is null)
            {
                Line = TerminalBuffer.WriteNewLineBeforeMidpoint(lineContents, false);
            }
            else
            {
                Line.Text = lineContents;
            }

            // For finished projects
            if (Finished)
            {
                if (CurrentTargetLine is not null)
                {
                    TerminalBuffer.DeleteLine(CurrentTargetLine.Id);
                }

                bool foundErrorOrWarning = false;

                foreach (MessageNode node in AdditionalDetails)
                {
                    if (node.Type != MessageNode.MessageType.HighPriorityMessage)
                    {
                        foundErrorOrWarning = true;
                    }

                    // Only delete high priority messages
                    if (node.Type != MessageNode.MessageType.HighPriorityMessage)
                    {
                        continue;
                    }

                    if (node.Line is not null)
                    {
                        TerminalBuffer.DeleteLine(node.Line.Id);
                    }
                }

                if (!foundErrorOrWarning && this.Line is not null)
                {
                    foreach (MessageNode node in AdditionalDetails)
                    {
                        int? id = node.Line?.Id;
                        if (id is not null)
                        {
                            TerminalBuffer.DeleteLine(id.Value);
                        }
                    }

                    TerminalBuffer.DeleteLine(this.Line.Id);
                }
            }

            // Current target details
            if (CurrentTargetNode is null)
            {
                return;
            }

            string currentTargetLineContents = $"    └── {CurrentTargetNode.TargetName} : {CurrentTargetNode.CurrentTaskNode?.TaskName ?? String.Empty}";
            if (CurrentTargetLine is null)
            {
                CurrentTargetLine = TerminalBuffer.WriteNewLineAfter(Line!.Id, currentTargetLineContents);
            }
            else
            {
                CurrentTargetLine.Text = currentTargetLineContents;
            }

            // Messages, warnings and errors
            foreach (MessageNode node in AdditionalDetails)
            {
                if (Finished && node.Type == MessageNode.MessageType.HighPriorityMessage)
                {
                    continue;
                }

                if (node.Line is null)
                {
                    node.Line = TerminalBuffer.WriteNewLineAfter(Line!.Id, "Message");
                }

                node.Log();
            }
        }

        public TargetNode AddTarget(TargetStartedEventArgs args)
        {
            CurrentTargetNode = new TargetNode(args);
            return CurrentTargetNode;
        }
        public TaskNode? AddTask(TaskStartedEventArgs args)
        {
            // Get target id
            int targetId = args.BuildEventContext!.TargetId;
            if (CurrentTargetNode?.Id == targetId)
            {
                return CurrentTargetNode.AddTask(args);
            }
            else
            {
                return null;
            }
        }
        public MessageNode? AddMessage(BuildMessageEventArgs args)
        {
            if (args.Importance != MessageImportance.High)
            {
                return null;
            }

            MessageCount++;
            MessageNode node = new MessageNode(args);
            // Add output executable path
            if (node.ProjectOutputExecutablePath is not null)
            {
                ProjectOutputExecutable = node.ProjectOutputExecutablePath;
            }

            AdditionalDetails.Add(node);
            return node;
        }
        public MessageNode? AddWarning(BuildWarningEventArgs args)
        {
            WarningCount++;
            MessageNode node = new MessageNode(args);
            AdditionalDetails.Add(node);
            TerminalBuffer.overallBuildState = TerminalBuffer.overallBuildState == OverallBuildState.Error ? OverallBuildState.Error : OverallBuildState.Warning;
            return node;
        }
        public MessageNode? AddError(BuildErrorEventArgs args)
        {
            ErrorCount++;
            MessageNode node = new MessageNode(args);
            AdditionalDetails.Add(node);
            TerminalBuffer.overallBuildState = OverallBuildState.Error;
            return node;
        }
    }
}
