// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.LiveLogger
{

    internal class MessageNode
    {
        // Use this to change the max lenngth (relative to screen size) of messages
        private static int MAX_LENGTH = 3 * Console.BufferWidth;
        public enum MessageType
        {
            HighPriorityMessage,
            Warning,
            Error,
            ProjectOutputMessage
        }
        public string Message;
        public TerminalBufferLine? Line;
        public MessageType Type;
        public string? Code;
        public string? FilePath;
        public int? LineNumber;
        public int? ColumnNumber;
        public string? ProjectOutputExecutablePath;
        public MessageNode(LazyFormattedBuildEventArgs args)
        {
            Message = args.Message ?? string.Empty;
            if (Message.Length > MAX_LENGTH)
            {
                Message = Message.Substring(0, MAX_LENGTH - 1) + "…";
            }
            // Get type
            switch (args)
            {
                case BuildMessageEventArgs message:
                    // Detect output messages
                    var finalOutputMarker = " -> ";
                    int i = message.Message!.IndexOf(finalOutputMarker, StringComparison.Ordinal);
                    if (i > 0)
                    {
                        Type = MessageType.ProjectOutputMessage;
                        ProjectOutputExecutablePath = message.Message!.Substring(i + finalOutputMarker.Length);
                    }
                    else
                    {
                        Type = MessageType.HighPriorityMessage;
                        Code = message.Subcategory;
                    }
                    break;
                case BuildWarningEventArgs warning:
                    Type = MessageType.Warning;
                    Code = warning.Code;
                    FilePath = warning.File;
                    LineNumber = warning.LineNumber;
                    ColumnNumber = warning.ColumnNumber;
                    break;
                case BuildErrorEventArgs error:
                    Type = MessageType.Error;
                    Code = error.Code;
                    FilePath = error.File;
                    LineNumber = error.LineNumber;
                    ColumnNumber = error.ColumnNumber;
                    break;
            }
        }

        public string ToANSIString()
        {
            switch (Type)
            {
                case MessageType.Warning:
                    return $"⚠️ {ANSIBuilder.Formatting.Color(
                        $"Warning {Code}: {FilePath}({LineNumber},{ColumnNumber}) {Message}",
                        ANSIBuilder.Formatting.ForegroundColor.Yellow)}";
                case MessageType.Error:
                    return $"❌ {ANSIBuilder.Formatting.Color(
                        $"Error {Code}: {FilePath}({LineNumber},{ColumnNumber}) {Message}",
                        ANSIBuilder.Formatting.ForegroundColor.Red)}";
                case MessageType.ProjectOutputMessage:
                    return $"⚙️ {ANSIBuilder.Formatting.Hyperlink(ProjectOutputExecutablePath!, Path.GetDirectoryName(ProjectOutputExecutablePath)!)}";
                case MessageType.HighPriorityMessage:
                default:
                    return $"ℹ️ {Code}{(Code is not null ? ": " : string.Empty)} {ANSIBuilder.Formatting.Italic(Message)}";
            }
        }

        // TODO: Rename to Log after LiveLogger's API becomes internal
        public void Log()
        {
            if (Line == null)
            {
                return;
            }

            Line.Text = $"    └── {ToANSIString()}";
        }
    }
}
