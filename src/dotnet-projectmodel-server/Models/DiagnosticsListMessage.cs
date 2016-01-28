// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    public class DiagnosticsListMessage
    {
        public DiagnosticsListMessage(IEnumerable<DiagnosticMessage> diagnostics) :
                this(diagnostics, frameworkData: null)
        {
        }

        public DiagnosticsListMessage(IEnumerable<DiagnosticMessage> diagnostics, FrameworkData frameworkData) :
                this(diagnostics.Select(msg => new DiagnosticMessageView(msg)).ToList(), frameworkData)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }
        }

        public DiagnosticsListMessage(IEnumerable<DiagnosticMessageView> diagnostics) :
                this(diagnostics, frameworkData: null)
        {
        }

        public DiagnosticsListMessage(IEnumerable<DiagnosticMessageView> diagnostics, FrameworkData frameworkData)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            Diagnostics = diagnostics;
            Errors = diagnostics.Where(msg => msg.Severity == DiagnosticMessageSeverity.Error).ToList();
            Warnings = diagnostics.Where(msg => msg.Severity == DiagnosticMessageSeverity.Warning).ToList();
            Framework = frameworkData;
        }

        public FrameworkData Framework { get; }

        [JsonIgnore]
        public IEnumerable<DiagnosticMessageView> Diagnostics { get; }

        public IList<DiagnosticMessageView> Errors { get; }

        public IList<DiagnosticMessageView> Warnings { get; }

        public override bool Equals(object obj)
        {
            var other = obj as DiagnosticsListMessage;

            return other != null &&
                Enumerable.SequenceEqual(Errors, other.Errors) &&
                Enumerable.SequenceEqual(Warnings, other.Warnings) &&
                object.Equals(Framework, other.Framework);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
