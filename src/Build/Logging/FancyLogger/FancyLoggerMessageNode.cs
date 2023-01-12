// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.FancyLogger
{ 

    public class FancyLoggerMessageNode
    {
        public string Message;
        public FancyLoggerBufferLine? Line;

        public FancyLoggerMessageNode(LazyFormattedBuildEventArgs args)
        {
            Message = args.Message ?? string.Empty;
        }

        public void Log()
        {
            if (Line == null) return;
            FancyLoggerBuffer.UpdateLine(Line.Id, $"    └── {ANSIBuilder.Formatting.Italic(Message)}");
        }
    }
}
