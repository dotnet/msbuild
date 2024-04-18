// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// The location of an XML node in a file.
    /// Any editing of the project XML through the MSBuild API's will invalidate locations in that XML until the XML is reloaded.
    /// </summary>
    /// <remarks>
    /// This object is IMMUTABLE, so that it can be passed around arbitrarily.
    /// DO NOT make these objects any larger. There are huge numbers of them and they are transmitted between nodes.
    /// </remarks>
    [Serializable]
    public abstract class ElementLocation : IElementLocation, ITranslatable, IImmutable
    {
        /// <summary>
        /// The singleton empty element location.
        /// </summary>
        private static readonly ElementLocation s_emptyElementLocation = new SmallElementLocation("", 0, 0);

        /// <summary>
        /// Gets the file from which this particular element originated.  It may
        /// differ from the ProjectFile if, for instance, it was part of
        /// an import or originated in a targets file.
        /// If not known, returns empty string.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public abstract string File
        {
            get;
        }

        /// <summary>
        /// Gets the line number where this element exists in its file.
        /// The first line is numbered 1.
        /// Zero indicates "unknown location".
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public abstract int Line
        {
            get;
        }

        /// <summary>
        /// Gets the column number where this element exists in its file.
        /// The first column is numbered 1.
        /// Zero indicates "unknown location".
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public abstract int Column
        {
            get;
        }

        /// <summary>
        /// Gets the location in a form suitable for replacement
        /// into a message.
        /// Example: "c:\foo\bar.csproj (12,34)"
        /// Calling this creates and formats a new string.
        /// PREFER TO PUT THE LOCATION INFORMATION AT THE START OF THE MESSAGE INSTEAD.
        /// Only in rare cases should the location go within the message itself.
        /// </summary>
        public string LocationString
        {
            get
            {
                int line = Line;
                int column = Column;
                return (line, column) switch
                {
                    (not 0, not 0) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("FileLocation", File, line, column),
                    (not 0, 0) => $"{File} ({line})",
                    _ => File,
                };
            }
        }

        /// <summary>
        /// Gets the empty element location.
        /// This is not to be used when something is "missing": that should have a null location.
        /// It is to be used for the project location when the project has not been given a name.
        /// In that case, it exists, but can't have a specific location.
        /// </summary>
        public static ElementLocation EmptyLocation
        {
            get { return s_emptyElementLocation; }
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            // Line and column are good enough
            return Line ^ Column;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is not IElementLocation that)
            {
                return false;
            }

            if (this.Line != that.Line || this.Column != that.Column)
            {
                return false;
            }

            if (!String.Equals(this.File, that.File, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return LocationString;
        }

        /// <inheritdoc />
        void ITranslatable.Translate(ITranslator translator)
        {
            ErrorUtilities.VerifyThrow(translator.Mode == TranslationDirection.WriteToStream, "write only");

            // Translate int, even if ushort is being used.
            // Internally, the translator uses a variable length (prefix) encoding.
            string file = File;
            int line = Line;
            int column = Column;
            translator.Translate(ref file);
            translator.Translate(ref line);
            translator.Translate(ref column);
        }

        /// <summary>
        /// Factory for serialization.
        /// Custom factory is needed because this class is abstract and uses a factory pattern.
        /// </summary>
        internal static ElementLocation FactoryForDeserialization(ITranslator translator)
        {
            string? file = null;
            int line = 0;
            int column = 0;
            translator.Translate(ref file);
            translator.Translate(ref line);
            translator.Translate(ref column);

            return Create(file, line, column);
        }

        /// <summary>
        /// Constructor for when we only know the file and nothing else.
        /// This is the case when we are creating a new item, for example, and it has
        /// not been evaluated from some XML.
        /// </summary>
        internal static ElementLocation Create(string file)
        {
            return Create(file, 0, 0);
        }

        /// <summary>
        /// Constructor for the case where we have most or all information.
        /// Numerical values must be 1-based, non-negative; 0 indicates unknown
        /// File may be null, indicating the file was not loaded from disk.
        /// </summary>
        /// <remarks>
        /// In AG there are 600 locations that have a file but zero line and column.
        /// In theory yet another derived class could be made for these to save 4 bytes each.
        /// </remarks>
        internal static ElementLocation Create(string? file, int line, int column)
        {
            // Combine line and column values with bitwise OR so we can perform various
            // checks on both values in a single comparison, reducing the amount of branching
            // in the code.
            int combinedValue = line | column;

            if (string.IsNullOrEmpty(file) && combinedValue == 0)
            {
                // When combinedValue is zero, it implies that both line and column are zero.
                return EmptyLocation;
            }

            // When combinedValue is negative, it implies that either line or column were negative
            ErrorUtilities.VerifyThrow(combinedValue > -1, "Use zero for unknown");

            file ??= "";

            // When combinedValue is less than a threshold, it implies that both line and column are less
            // than that threshold.
            if (combinedValue <= ushort.MaxValue)
            {
                return new SmallElementLocation(file, line, column);
            }

            return new RegularElementLocation(file, line, column);
        }

        /// <summary>
        /// Rarer variation for when the line and column won't each fit in a ushort.
        /// </summary>
        private class RegularElementLocation(string file, int line, int column) : ElementLocation
        {
            /// <inheritdoc />
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override string File { get; } = file;

            /// <inheritdoc />
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override int Line { get; } = line;

            /// <inheritdoc />
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override int Column { get; } = column;
        }

        /// <summary>
        /// For when the line and column each fit in a short - under 65536
        /// (almost always will: microsoft.common.targets is less than 5000 lines long)
        /// When loading Australian Government, for example, there are over 31,000 ElementLocation
        /// objects so this saves 4 bytes each = 123KB
        ///
        /// A "very small" variation that used two bytes (or halves of a short) would fit about half of them
        /// and save 4 more bytes each, but the CLR packs each field to 4 bytes, so it isn't actually any smaller.
        /// </summary>
        private class SmallElementLocation : ElementLocation
        {
            /// <summary>
            /// Packs both the line and column values into a single four-byte element.
            /// The high two bytes are the line, and low two bytes are the column.
            /// </summary>
            /// <remarks>
            /// If we had two <see cref="ushort"/> fields, the CLR would pad them each to
            /// four-byte boundaries, meaning no space would actually be saved here.
            /// So instead, we pack them manually.
            /// </remarks>
            private int packedData;

            /// <summary>
            /// Constructor for the case where we have most or all information.
            /// Numerical values must be 1-based, non-negative; 0 indicates unknown
            /// File may empty, indicating the file was not loaded from disk.
            /// </summary>
            internal SmallElementLocation(string file, int line, int column)
            {
                File = file;
                packedData = (line << 16) | column;
            }

            /// <inheritdoc />
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override string File { get; }

            /// <inheritdoc />
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override int Line
            {
                get { return (packedData >> 16) & 0xFFFF; }
            }

            /// <inheritdoc />
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override int Column
            {
                get { return packedData & ushort.MaxValue; }
            }
        }
    }
}
