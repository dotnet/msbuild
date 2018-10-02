using System;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// A bitmask to specify which fields on a BuildEventArgs object are present; used in serialization
    /// </summary>
    [Flags]
    internal enum BuildEventArgsFieldFlags
    {
        None = 0,
        BuildEventContext = 1 << 0,
        HelpHeyword = 1 << 1,
        Message = 1 << 2,
        SenderName = 1 << 3,
        ThreadId = 1 << 4,
        Timestamp = 1 << 5,
        Subcategory = 1 << 6,
        Code = 1 << 7,
        File = 1 << 8,
        ProjectFile = 1 << 9,
        LineNumber = 1 << 10,
        ColumnNumber = 1 << 11,
        EndLineNumber = 1 << 12,
        EndColumnNumber = 1 << 13
    }
}
