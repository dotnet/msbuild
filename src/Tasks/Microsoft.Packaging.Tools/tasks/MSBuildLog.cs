// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
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
