using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Represents a collective set of common properties on BuildEventArgs. Used for deserialization.
    /// </summary>
    internal class BuildEventArgsFields
    {
        public BuildEventArgsFieldFlags Flags { get; set; }

        public string Message { get; set; }
        public BuildEventContext BuildEventContext { get; set; }
        public int ThreadId { get; set; }
        public string HelpKeyword { get; set; }
        public string SenderName { get; set; }
        public DateTime Timestamp { get; set; }

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
