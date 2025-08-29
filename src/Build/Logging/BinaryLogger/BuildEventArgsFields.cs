﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Represents a collective set of common properties on BuildEventArgs. Used for deserialization.
    /// </summary>
    internal class BuildEventArgsFields
    {
        public BuildEventArgsFieldFlags Flags { get; set; }

        public string Message { get; set; }
        public object[] Arguments { get; set; }
        public BuildEventContext BuildEventContext { get; set; }
        public int ThreadId { get; set; }
        public string HelpKeyword { get; set; }
        public string SenderName { get; set; }
        public DateTime Timestamp { get; set; }
        public MessageImportance Importance { get; set; } = MessageImportance.Low;

        public string Subcategory { get; set; }
        public string Code { get; set; }
        public string File { get; set; }
        public string ProjectFile { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public int EndLineNumber { get; set; }
        public int EndColumnNumber { get; set; }
    }
}
