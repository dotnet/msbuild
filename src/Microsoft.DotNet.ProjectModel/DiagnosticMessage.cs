// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel
{
    /// <summary>
    /// Represents a single diagnostic message, such as a compilation error or a project.json parsing error.
    /// </summary>
    public class DiagnosticMessage
    {
        public DiagnosticMessage(string errorCode, string message, string filePath, DiagnosticMessageSeverity severity)
            : this(errorCode, message, filePath, severity, startLine: 1, startColumn: 0)
        { }

        public DiagnosticMessage(string errorCode, string message, string filePath, DiagnosticMessageSeverity severity, int startLine, int startColumn)
                : this(
                    errorCode,
                    message,
                    $"{filePath}({startLine},{startColumn}): {severity.ToString().ToLowerInvariant()} {errorCode}: {message}",
                    filePath,
                    severity,
                    startLine,
                    startColumn,
                    endLine: startLine,
                    endColumn: startColumn,
                    source: null)
        { }

        public DiagnosticMessage(string errorCode, string message, string filePath, DiagnosticMessageSeverity severity, int startLine, int startColumn, object source)
                : this(
                    errorCode,
                    message,
                    $"{filePath}({startLine},{startColumn}): {severity.ToString().ToLowerInvariant()} {errorCode}: {message}",
                    filePath,
                    severity,
                    startLine,
                    startColumn,
                    endLine: startLine,
                    endColumn: startColumn,
                    source: source)
        { }

        public DiagnosticMessage(string errorCode, string message, string formattedMessage, string filePath, 
                                 DiagnosticMessageSeverity severity, int startLine, int startColumn, int endLine, int endColumn)
            : this(errorCode, 
                   message,
                   formattedMessage,
                   filePath,
                   severity,
                   startLine,
                   startColumn,
                   endLine,
                   endColumn,
                   source: null)
        {
        }

        public DiagnosticMessage(
            string errorCode,
            string message,
            string formattedMessage,
            string filePath,
            DiagnosticMessageSeverity severity,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            object source)
        {
            ErrorCode = errorCode;
            Message = message;
            SourceFilePath = filePath;
            Severity = severity;
            StartLine = startLine;
            EndLine = endLine;
            StartColumn = startColumn;
            EndColumn = endColumn;
            FormattedMessage = formattedMessage;
            Source = source;
        }

        /// <summary>
        /// The moniker associated with the error message
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Path of the file that produced the message.
        /// </summary>
        public string SourceFilePath { get; }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the <see cref="DiagnosticMessageSeverity"/>.
        /// </summary>
        public DiagnosticMessageSeverity Severity { get; }

        /// <summary>
        /// Gets the one-based line index for the start of the compilation error.
        /// </summary>
        public int StartLine { get; }

        /// <summary>
        /// Gets the zero-based column index for the start of the compilation error.
        /// </summary>
        public int StartColumn { get; }

        /// <summary>
        /// Gets the one-based line index for the end of the compilation error.
        /// </summary>
        public int EndLine { get; }

        /// <summary>
        /// Gets the zero-based column index for the end of the compilation error.
        /// </summary>
        public int EndColumn { get; }

        /// <summary>
        /// Gets the formatted error message.
        /// </summary>
        public string FormattedMessage { get; }

        /// <summary>
        /// Gets the source of this message
        /// </summary>
        public object Source { get; }
    }
}