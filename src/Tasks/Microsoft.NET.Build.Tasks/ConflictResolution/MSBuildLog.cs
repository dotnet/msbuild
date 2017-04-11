// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    internal class MSBuildLog : ILog
    {
        private TaskLoggingHelper logger;
        public MSBuildLog(TaskLoggingHelper logger)
        {
            this.logger = logger;
        }

        public void LogError(string message, params object[] messageArgs)
        {
            logger.LogError(message, messageArgs);
        }

        public void LogMessage(string message, params object[] messageArgs)
        {
            logger.LogMessage(message, messageArgs);
        }

        public void LogMessage(LogImportance importance, string message, params object[] messageArgs)
        {
            logger.LogMessage((MessageImportance)importance, message, messageArgs);
        }

        public void LogWarning(string message, params object[] messageArgs)
        {
            logger.LogWarning(message, messageArgs);
        }
    }
}
