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
        public string Message;
        public FancyLoggerBufferLine? Line;

        public FancyLoggerMessageNode(LazyFormattedBuildEventArgs args)
        {
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
            FancyLoggerBuffer.UpdateLine(Line.Id, $"    └── {ANSIBuilder.Formatting.Italic(Message)}");
        }
    }
}
