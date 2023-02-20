// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.Ansi;

namespace Microsoft.Build.Logging
{
    internal class MessageNode
    {
        private const string FinalOutputMarker = " -> ";

        /// <summary>
        /// Use this to change the max lenngth (relative to screen size) of messages.
        /// </summary>
        private static readonly int MaxLength = 3 * Console.BufferWidth;

        private readonly string _message;

        private readonly string _code = string.Empty;

        private readonly int _columnNumber;

        private readonly int _lineNumber;

        private readonly string _filePath = string.Empty;

        internal MessageNode(LazyFormattedBuildEventArgs args, string? projectOutputExecutablePath = null)
        {
            _message = args.Message ?? string.Empty;
            if (_message.Length > MaxLength)
            {
                _message = _message.Substring(0, MaxLength - 1) + "…";
            }

            if (args is BuildMessageEventArgs message)
            {
                // Detect output messages
                int i = message.Message!.IndexOf(FinalOutputMarker, StringComparison.Ordinal);
                if (i > 0)
                {
                    NodeType = MessageNodeType.ProjectOutputMessage;
                    ProjectOutputExecPath = message.Message!.Substring(i + FinalOutputMarker.Length);
                }
                else
                {
                    NodeType = MessageNodeType.HighPriorityMessage;
                    _code = message.Subcategory;
                }
            }
            else if (args is BuildWarningEventArgs warning)
            {
                NodeType = MessageNodeType.Warning;
                _code = warning.Code;
                _filePath = warning.File;
                _lineNumber = warning.LineNumber;
                _columnNumber = warning.ColumnNumber;
            }
            else if (args is BuildErrorEventArgs error)
            {
                NodeType = MessageNodeType.Error;
                _code = error.Code;
                _filePath = error.File;
                _lineNumber = error.LineNumber;
                _columnNumber = error.ColumnNumber;
            }

            ProjectOutputExecPath = projectOutputExecutablePath ?? string.Empty;
        }

        internal MessageNodeType NodeType { get; }

        internal TerminalBufferLine? Line { get; set; }

        internal string ProjectOutputExecPath { get; } = string.Empty;

        internal string ToAnsiString() =>
            NodeType switch
            {
                MessageNodeType.Warning =>
                $"⚠️ {AnsiBuilder.Formatter.Color($"Warning {_code}: {_filePath}({_lineNumber},{_columnNumber}) {_message}", ForegroundColor.Yellow)}",
                MessageNodeType.Error =>
                $"❌ {AnsiBuilder.Formatter.Color($"Error {_code}: {_filePath}({_lineNumber},{_columnNumber}) {_message}", ForegroundColor.Red)}",
                MessageNodeType.ProjectOutputMessage =>
                $"⚙️ {AnsiBuilder.Formatter.Hyperlink(ProjectOutputExecPath!, Path.GetDirectoryName(ProjectOutputExecPath)!)}",
                MessageNodeType.HighPriorityMessage => $"ℹ️ {_code}{(_code is not null ? ": " : string.Empty)} {AnsiBuilder.Formatter.Italic(_message)}",
                _ => $"ℹ️ {_code}{(_code is not null ? ": " : string.Empty)} {AnsiBuilder.Formatter.Italic(_message)}",
            };

        // TODO: Rename to Log after LiveLogger's API becomes internal
        internal void Log()
        {
            if (Line != null)
            {
                Line.Text = $"    └── {ToAnsiString()}";
            }
        }
    }
}
