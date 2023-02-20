// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.Ansi;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    internal class ProjectNode
    {
        private const string PropertyTargetFramework = "_targetFramework";

        private readonly List<MessageNode> _additionalDetails = new();

        private readonly string _targetFramework;

        private readonly string _projectPath;

        private readonly TerminalBufferLine? _currentTargetLine;

        private string? _projectOutputExe;

        private volatile int _messageCount = 0;

        private volatile int _finishedTargetsCount = 0;

        private int _warningCount = 0;

        private int _errorCount = 0;

        /// <summary>
        /// <see cref="TerminalBufferLine"/> to display project info.
        /// </summary>
        private TerminalBufferLine? _terminalBufferLine;

        private TargetNode? _currentTargetNode;

        internal ProjectNode(ProjectStartedEventArgs args)
        {
            _projectPath = args.ProjectFile!;
            _targetFramework = args.GlobalProperties != null
                && args.GlobalProperties.TryGetValue(PropertyTargetFramework, out string? value)
                ? value
                : "";
        }

        internal int AdditionalDetailsCount => _additionalDetails.Count;

        internal string ProjectOutputExe => _projectOutputExe ?? string.Empty;

        internal bool Finished { get; set; } = false;

        internal int FinishedTargets => _finishedTargetsCount;

        /// <summary>
        /// Gets or sets a value indicating whether bool if node should rerender.
        /// </summary>
        internal bool ShouldRerender { get; set; } = true;

        internal int WarningCount => _warningCount;

        internal int ErrorCount => _errorCount;

        internal string ToAnsiString()
        {
            ForegroundColor color = GetFormattingColor();
            return GetIconString() +
                " " +
                AnsiBuilder.Formatter.Color(AnsiBuilder.Formatter.Bold(GetUnambiguousPath(_projectPath)), color) +
                " " +
                AnsiBuilder.Formatter.Inverse(_targetFramework);
        }

        internal IEnumerable<MessageNode> GetAdditionalDetails()
        {
            foreach (MessageNode msg in _additionalDetails)
            {
                yield return msg;
            }
        }

        // TODO: Rename to Render() after LiveLogger's API becomes internal
        internal void Log()
        {
            if (!ShouldRerender)
            {
                return;
            }

            ShouldRerender = false;

            // Create or update line
            SetTerminalBufferLineText(AnsiBuilder.Aligner.SpaceBetween(ToAnsiString(), $"({_messageCount} ℹ️, {_warningCount} ⚠️, {_errorCount} ❌)", Console.BufferWidth - 1));

            // For finished projects
            if (Finished)
            {
                if (_currentTargetLine is not null)
                {
                    TerminalBuffer.DeleteLine(_currentTargetLine.Id);
                }

                foreach (MessageNode node in _additionalDetails.ToList())
                {
                    // Only delete high priority messages
                    if (node.NodeType is MessageNodeType.HighPriorityMessage
                        && node.Line is not null)
                    {
                        TerminalBuffer.DeleteLine(node.Line.Id);
                    }
                }
            }

            // Current target details
            if (_currentTargetNode != null)
            {
                string targetLineContents = $"    └── {_currentTargetNode.TargetName} : {_currentTargetNode.CurrentTaskNode?.TaskName ?? string.Empty}";
                SetTerminalBufferLineText(_terminalBufferLine!.Id, targetLineContents);

                // Messages, warnings and errors
                foreach (MessageNode node in _additionalDetails)
                {
                    if (Finished
                        && node.NodeType is not MessageNodeType.HighPriorityMessage
                        && node.Line is null)
                    {
                        node.Line = TerminalBuffer.WriteNewLineAfter(_terminalBufferLine!.Id, "Message");
                        node.Log();
                    }
                }
            }
        }

        internal void IncrementFinishedTargetsCount() => _ = Interlocked.Increment(ref _finishedTargetsCount);

        internal TargetNode AddTarget(TargetStartedEventArgs args)
        {
            _currentTargetNode = new TargetNode(args);
            return _currentTargetNode;
        }

        internal TaskNode? AddTask(TaskStartedEventArgs args) =>
            _currentTargetNode?.Id == args.BuildEventContext!.TargetId
            ? _currentTargetNode.AddTask(args)
            : null;

        internal MessageNode? AddMessage(BuildMessageEventArgs args)
        {
            if (args.Importance == MessageImportance.High)
            {
                _ = Interlocked.Add(ref _messageCount, 1);
                MessageNode node = new(args);

                // Add output executable path
                if (node.ProjectOutputExecPath is not null)
                {
                    _projectOutputExe = node.ProjectOutputExecPath;
                }

                _additionalDetails.Add(node);
                return node;
            }

            return null;
        }

        internal MessageNode? AddWarning(BuildWarningEventArgs args) => AddMessageNode(x => new MessageNode(x), args, ref _warningCount);

        internal MessageNode? AddError(BuildErrorEventArgs args) => AddMessageNode(x => new MessageNode(x), args, ref _errorCount);

        /// <summary>
        /// Given a list of paths, this method will get the shortest not ambiguous path for a project.
        /// </summary>
        /// <example>for `/users/documents/foo/project.csproj` and `/users/documents/bar/project.csproj`, the respective non ambiguous paths would be `foo/project.csproj` and `bar/project.csproj`
        /// Still work in progress...
        /// </example>
        private static string GetUnambiguousPath(string path) => Path.GetFileName(path);

        private MessageNode? AddMessageNode<T>(Func<T, MessageNode> factory, T args, ref int count)
        {
            _ = Interlocked.Add(ref count, 1);
            MessageNode node = factory(args);
            _additionalDetails.Add(node);
            return node;
        }

        private void SetTerminalBufferLineText(string text)
        {
            if (_terminalBufferLine is null)
            {
                _terminalBufferLine = TerminalBuffer.WriteNewLine(text, false);
            }
            else
            {
                _terminalBufferLine.Text = text;
            }
        }

        private void SetTerminalBufferLineText(int lineId, string text)
        {
            if (_terminalBufferLine is null)
            {
                _terminalBufferLine = TerminalBuffer.WriteNewLineAfter(lineId, text);
            }
            else
            {
                _terminalBufferLine.Text = text;
            }
        }

        private string GetIconString()
        {
            if (Finished && _warningCount + _errorCount == 0)
            {
                return "✓";
            }
            else if (_errorCount > 0)
            {
                return "X";
            }
            else if (_warningCount > 0)
            {
                return "✓";
            }

            return $"{AnsiBuilder.Formatter.Blinking(AnsiBuilder.Graphics.Spinner())} ";
        }

        private ForegroundColor GetFormattingColor()
        {
            if (Finished && _warningCount + _errorCount == 0)
            {
                return ForegroundColor.Green;
            }
            else if (_errorCount > 0)
            {
                return ForegroundColor.Red;
            }
            else if (_warningCount > 0)
            {
                return ForegroundColor.Yellow;
            }

            return ForegroundColor.Default;
        }
    }
}
