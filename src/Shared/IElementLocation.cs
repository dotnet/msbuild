// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;

#nullable disable

namespace Microsoft.Build.Shared
{
    internal interface IElementLocation : IMsBuildElementLocation, ITranslatable { }

    /// <summary>
    /// Represents the location information for error reporting purposes.  This is normally used to
    /// associate a run-time error with the original XML.
    /// This is not used for arbitrary errors from tasks, which store location in a BuildXXXXEventArgs.
    /// All implementations should be IMMUTABLE.
    /// Any editing of the project XML through the MSBuild API's will invalidate locations in that XML until the XML is reloaded.
    /// </summary>
    /// <remarks>
    /// This is currently internal - but it is prepared to be made public once it will be needed by other public BuildCheck OM
    /// (e.g. by property read/write OM)
    /// </remarks>
    public interface IMsBuildElementLocation
    {
        /// <summary>
        /// The file from which this particular element originated.  It may
        /// differ from the ProjectFile if, for instance, it was part of
        /// an import or originated in a targets file.
        /// Should always have a value.
        /// If not known, returns empty string.
        /// </summary>
        string File
        {
            get;
        }

        /// <summary>
        /// The line number where this element exists in its file.
        /// The first line is numbered 1.
        /// Zero indicates "unknown location".
        /// </summary>
        int Line
        {
            get;
        }

        /// <summary>
        /// The column number where this element exists in its file.
        /// The first column is numbered 1.
        /// Zero indicates "unknown location".
        /// </summary>
        int Column
        {
            get;
        }

        /// <summary>
        /// The location in a form suitable for replacement
        /// into a message.
        /// </summary>
        string LocationString
        {
            get;
        }
    }
}
