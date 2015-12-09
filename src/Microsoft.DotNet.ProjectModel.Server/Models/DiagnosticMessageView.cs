// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    public class DiagnosticMessageView
    {
        public DiagnosticMessageView(DiagnosticMessage data)
        {
            ErrorCode = data.ErrorCode;
            SourceFilePath = data.SourceFilePath;
            Message = data.Message;
            Severity = data.Severity;
            StartLine = data.StartLine;
            StartColumn = data.StartColumn;
            EndLine = data.EndLine;
            EndColumn = data.EndColumn;
            FormattedMessage = data.FormattedMessage;

            var description = data.Source as LibraryDescription;
            if (description != null)
            {
                Source = new
                {
                    Name = description.Identity.Name,
                    Version = description.Identity.Version?.ToString()
                };
            }
        }

        public string ErrorCode { get; }

        public string SourceFilePath { get; }

        public string Message { get; }

        public DiagnosticMessageSeverity Severity { get; }

        public int StartLine { get; }

        public int StartColumn { get; }

        public int EndLine { get; }

        public int EndColumn { get; }

        public string FormattedMessage { get; }

        public object Source { get; }

        public override bool Equals(object obj)
        {
            var other = obj as DiagnosticMessageView;

            return other != null &&
                   Severity == other.Severity &&
                   StartLine == other.StartLine &&
                   StartColumn == other.StartColumn &&
                   EndLine == other.EndLine &&
                   EndColumn == other.EndColumn &&
                   string.Equals(ErrorCode, other.ErrorCode, StringComparison.Ordinal) &&
                   string.Equals(SourceFilePath, other.SourceFilePath, StringComparison.Ordinal) &&
                   string.Equals(Message, other.Message, StringComparison.Ordinal) &&
                   object.Equals(Source, other.Source);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
