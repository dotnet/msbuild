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

        public ITaskItem[] GetDiagnosticMessages() => _diagnosticMessages.ToArray();

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
            string packageId)
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

            return diagnostic;
        }
    }
}
