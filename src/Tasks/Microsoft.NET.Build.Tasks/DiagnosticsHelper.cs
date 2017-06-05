// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Writes diagnostic messages to the task log and creates diagnostic task items 
    /// that can be returned from a task
    /// </summary>
    internal sealed class DiagnosticsHelper
    {
        private readonly List<ITaskItem> _diagnosticMessages = new List<ITaskItem>();
        private readonly ILog _log;

        public DiagnosticsHelper(ILog log)
        {
            _log = log;
        }

        public ITaskItem[] GetDiagnosticMessages() => _diagnosticMessages.ToArray();

        public ITaskItem Add(string diagnosticCode, string message, string filePath, DiagnosticMessageSeverity severity)
            => Add(diagnosticCode, message, filePath, severity, startLine: 1, startColumn: 0);

        public ITaskItem Add(string diagnosticCode, string message, string filePath, DiagnosticMessageSeverity severity, int startLine, int startColumn)
            => Add(
                diagnosticCode,
                message,
                filePath,
                severity,
                startLine,
                startColumn,
                targetFrameworkMoniker: null,
                packageId: null);

        public ITaskItem Add(
            string diagnosticCode,
            string message,
            string filePath,
            DiagnosticMessageSeverity severity,
            int startLine,
            int startColumn,
            string targetFrameworkMoniker,
            string packageId,
            bool logToMSBuild = true)
            => Add(
                diagnosticCode,
                message,
                filePath,
                severity,
                startLine,
                startColumn,
                endLine: startLine,
                endColumn: startColumn,
                targetFrameworkMoniker: targetFrameworkMoniker,
                packageId: packageId,
                logToMSBuild: logToMSBuild);

        public ITaskItem Add(
            string diagnosticCode,
            string message,
            string filePath,
            DiagnosticMessageSeverity severity,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            string targetFrameworkMoniker,
            string packageId,
            bool logToMSBuild = true)
        {
            string itemspec =
                (string.IsNullOrEmpty(targetFrameworkMoniker) ? string.Empty : $"{targetFrameworkMoniker}/") +
                (string.IsNullOrEmpty(packageId) ? string.Empty : $"{packageId}/") + 
                diagnosticCode;

            var diagnostic = new TaskItem(itemspec, new Dictionary<string, string>
            {
                { MetadataKeys.DiagnosticCode, diagnosticCode },
                { MetadataKeys.Message,        message },
                { MetadataKeys.FilePath,       filePath ?? string.Empty },
                { MetadataKeys.Severity,       severity.ToString() },

                { MetadataKeys.StartLine,      startLine.ToString() },
                { MetadataKeys.StartColumn,    startColumn.ToString() },
                { MetadataKeys.EndLine,        endLine.ToString() },
                { MetadataKeys.EndColumn,      endColumn.ToString() },

                { MetadataKeys.ParentTarget,   targetFrameworkMoniker ?? string.Empty },
                { MetadataKeys.ParentPackage,  packageId ?? string.Empty },
            });

            _diagnosticMessages.Add(diagnostic);

            if (logToMSBuild)
            {
                LogToMSBuild(diagnosticCode, message, filePath, severity, startLine, startColumn, endLine, endColumn);
            }

            return diagnostic;
        }

        private void LogToMSBuild(string diagnosticCode, string message, string filePath, DiagnosticMessageSeverity severity, int startLine, int startColumn, int endLine, int endColumn)
        {
            switch (severity)
            {
                case DiagnosticMessageSeverity.Error:
                    _log.LogError(
                        subcategory: null,
                        errorCode: diagnosticCode,
                        helpKeyword: null,
                        file: filePath,
                        lineNumber: startLine,
                        columnNumber: startColumn,
                        endLineNumber: endLine,
                        endColumnNumber: endColumn,
                        message: message);
                    break;

                case DiagnosticMessageSeverity.Warning:
                    _log.LogWarning(
                        subcategory: null,
                        warningCode: diagnosticCode,
                        helpKeyword: null,
                        file: filePath,
                        lineNumber: startLine,
                        columnNumber: startColumn,
                        endLineNumber: endLine,
                        endColumnNumber: endColumn,
                        message: message);
                    break;

                case DiagnosticMessageSeverity.Info:
                    _log.LogMessage(
                        subcategory: null,
                        code: diagnosticCode,
                        helpKeyword: null,
                        file: filePath,
                        lineNumber: startLine,
                        columnNumber: startColumn,
                        endLineNumber: endLine,
                        endColumnNumber: endColumn,
                        importance: MessageImportance.Normal,
                        message: message);
                    break;
            }
        }
    }
}
