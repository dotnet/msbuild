// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

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
        public string? Code;
        public string? FilePath;
        public int? LineNumber;
        public int? ColumnNumber;
        public FancyLoggerMessageNode(LazyFormattedBuildEventArgs args)
        {
            Message = args.Message ?? string.Empty;
            // Get type
            switch (args)
            {
                case BuildMessageEventArgs:
                    Type = MessageType.HighPriorityMessage;
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
                case MessageType.HighPriorityMessage:
                default:
                    return $"ℹ️ {ANSIBuilder.Formatting.Italic(Message)}";
            }
        }

        public void Log()
        {
            if (Line == null) return;
            FancyLoggerBuffer.UpdateLine(Line.Id, $"    └── {ToANSIString()}");
        }
    }
}
