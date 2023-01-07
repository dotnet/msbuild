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

    public class FancyLoggerMessageNode
    {
        public enum MessageType
        {
            HighPriorityMessage,
            Warning,
            Error
        }
        public string Message;
        public FancyLoggerBufferLine? Line;
        public MessageType Type;
        //
        public string? Code;
        public string? FilePath;
        public int? LineNumber;
        public int? ColumnNumber;
        public FancyLoggerMessageNode(LazyFormattedBuildEventArgs args)
        {
            // Get type
            if (args is BuildMessageEventArgs message)
            {
                Type = MessageType.HighPriorityMessage;
            }
            else if (args is BuildWarningEventArgs warning)
            {
                Type = MessageType.Warning;
            }
            else if (args is BuildErrorEventArgs error)
            {
                Type = MessageType.Error;
                Code = error.Code;
                FilePath = error.File;
                LineNumber = error.LineNumber;
                ColumnNumber = error.ColumnNumber;
            }

                // TODO: Replace
                if (args.Message == null)
            {
                Message = string.Empty;
            }
            else if (args.Message.Length > Console.WindowWidth - 1)
            {
                Message = args.Message.Substring(0, Console.WindowWidth - 1);
            }
            else
            {
                Message = args.Message;
            }
        }

        public void Log()
        {
            if (Line == null) return;
            // Get color
            if (Type == MessageType.HighPriorityMessage)
            {
                FancyLoggerBuffer.UpdateLine(Line.Id, $"    └── {ANSIBuilder.Formatting.Italic(Message)}");
            }
            else if (Type == MessageType.Warning)
            {
                FancyLoggerBuffer.UpdateLine(Line.Id, $"    └── {ANSIBuilder.Formatting.Color(
                    ANSIBuilder.Formatting.Italic($"{ANSIBuilder.Formatting.Dim("Warning:")} {Message}"),
                    ANSIBuilder.Formatting.ForegroundColor.Yellow
                )}");
            } else if (Type == MessageType.Error)
            {
                FancyLoggerBuffer.UpdateLine(Line.Id, $"    └── {ANSIBuilder.Formatting.Color(
                    ANSIBuilder.Formatting.Italic($"Error {Code}: {FilePath}({LineNumber},{ColumnNumber}) {Message}"),
                    ANSIBuilder.Formatting.ForegroundColor.Red
                )}");
            }
        }
    }
}
