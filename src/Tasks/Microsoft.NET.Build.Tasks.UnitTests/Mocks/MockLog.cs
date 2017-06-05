// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class MockLog : ILog
    {
        public const string ErrorPrefix = "[ERROR]";
        public const string WarningPrefix = "[WARNING]";
        public const string MessagePrefix = "[MESSAGE]";

        // track unformatted messages
        public List<string> Messages { get; } = new List<string>();

        public void LogError(string message, params object[] messageArgs)
        {
            Messages.Add($"{ErrorPrefix}: {message}");
        }

        public void LogMessage(string message, params object[] messageArgs)
        {
            Messages.Add($"{MessagePrefix}: {message}");
        }

        public void LogMessage(LogImportance importance, string message, params object[] messageArgs)
        {
            Messages.Add($"{MessagePrefix}: {message}");
        }

        public void LogWarning(string message, params object[] messageArgs)
        {
            Messages.Add($"{WarningPrefix}: {message}");
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
            Messages.Add($"{ErrorPrefix}: {message}");
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
            Messages.Add($"{WarningPrefix}: {message}");
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
            Messages.Add($"{MessagePrefix}: {message}");
        }
    }
}
