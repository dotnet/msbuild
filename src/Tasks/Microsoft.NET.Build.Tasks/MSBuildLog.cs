// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    internal class MSBuildLog : ILog
    {
        private readonly TaskLoggingHelper _logger;

        public MSBuildLog(TaskLoggingHelper logger)
        {
            _logger = logger;
        }

        public void LogError(string message, params object[] messageArgs)
        {
            _logger.LogError(message, messageArgs);
        }

        public void LogMessage(string message, params object[] messageArgs)
        {
            _logger.LogMessage(message, messageArgs);
        }

        public void LogMessage(LogImportance importance, string message, params object[] messageArgs)
        {
            _logger.LogMessage((MessageImportance)importance, message, messageArgs);
        }

        public void LogWarning(string message, params object[] messageArgs)
        {
            _logger.LogWarning(message, messageArgs);
        }

        public void LogError(
            string subcategory,
            string errorCode,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            params object[] messageArgs)
        {
            _logger.LogError(
                subcategory,
                errorCode,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                messageArgs);
        }

        public void LogWarning(
            string subcategory,
            string warningCode,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            params object[] messageArgs)
        {
            _logger.LogWarning(
                subcategory,
                warningCode,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                messageArgs);
        }

        public void LogMessage(
            string subcategory,
            string code,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            MessageImportance importance,
            string message,
            params object[] messageArgs)
        {
            _logger.LogMessage(
                subcategory,
                code,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                importance,
                message,
                messageArgs);
        }
    }
}
