// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using System;
using System.Diagnostics;

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
        private static ElementLocation s_emptyElementLocation = new SmallElementLocation(null, 0, 0);

        /// <summary>
        /// The file from which this particular element originated.  It may
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
        /// The line number where this element exists in its file.
        /// The first line is numbered 1.
        /// Zero indicates "unknown location".
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public abstract int Line
        {
            get;
        }

        /// <summary>
        /// The column number where this element exists in its file.
        /// The first column is numbered 1.
        /// Zero indicates "unknown location".
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public abstract int Column
        {
            get;
        }

        /// <summary>
        /// The location in a form suitable for replacement
        /// into a message.
        /// Example: "c:\foo\bar.csproj (12,34)"
        /// Calling this creates and formats a new string.
        /// PREFER TO PUT THE LOCATION INFORMATION AT THE START OF THE MESSAGE INSTEAD.
        /// Only in rare cases should the location go within the message itself.
        /// </summary>
        public string LocationString
        {
            get { return GetLocationString(File, Line, Column); }
        }

        /// <summary>
        /// Gets the empty element location.
        /// This is not to be used when something is "missing": that should have a null location.
        /// It is to be used for the project location when the project has not been given a name.
        /// In that case, it exists, but can't have a specific location.
        /// </summary>
        internal static ElementLocation EmptyLocation
        {
            get { return s_emptyElementLocation; }
        }

        /// <summary>
        /// Get reasonable hash code.
        /// </summary>
        public override int GetHashCode()
        {
            // Line and column are good enough
            return Line.GetHashCode() ^ Column.GetHashCode();
        }

        /// <summary>
        /// Override Equals so that identical
        /// fields imply equal objects.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            IElementLocation that = obj as IElementLocation;

            if (that == null)
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

        /// <summary>
        /// Location of element.
        /// </summary>
        public override string ToString()
        {
            return LocationString;
        }

        /// <summary>
        /// Writes the packet to the serializer.
        /// Always send as ints, even if ushorts are being used: otherwise it'd
        /// need a byte to discriminate and the savings would be microscopic.
        /// </summary>
        void ITranslatable.Translate(ITranslator translator)
        {
            ErrorUtilities.VerifyThrow(translator.Mode == TranslationDirection.WriteToStream, "write only");

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
            string file = null;
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
        internal static ElementLocation Create(string file, int line, int column)
        {
            if (string.IsNullOrEmpty(file) && line == 0 && column == 0)
            {
                return EmptyLocation;
            }

            if (line <= 65535 && column <= 65535)
            {
                return new ElementLocation.SmallElementLocation(file, line, column);
            }

            return new ElementLocation.RegularElementLocation(file, line, column);
        }

        /// <summary>
        /// The location in a form suitable for replacement
        /// into a message.
        /// Example: "c:\foo\bar.csproj (12,34)"
        /// Calling this creates and formats a new string.
        /// PREFER TO PUT THE LOCATION INFORMATION AT THE START OF THE MESSAGE INSTEAD.
        /// Only in rare cases should the location go within the message itself.
        /// </summary>
        private static string GetLocationString(string file, int line, int column)
        {
            string locationString = String.Empty;
            if (line != 0 && column != 0)
            {
                locationString = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("FileLocation", file, line, column);
            }
            else if (line != 0)
            {
                locationString = file + " (" + line + ")";
            }
            else
            {
                locationString = file;
            }

            return locationString;
        }

        /// <summary>
        /// Rarer variation for when the line and column won't each fit in a ushort.
        /// </summary>
        private class RegularElementLocation : ElementLocation
        {
            /// <summary>
            /// The source file.
            /// </summary>
            private string file;

            /// <summary>
            /// The source line.
            /// </summary>
            private int line;

            /// <summary>
            /// The source column.
            /// </summary>
            private int column;

            /// <summary>
            /// Constructor for the case where we have most or all information.
            /// Numerical values must be 1-based, non-negative; 0 indicates unknown
            /// File may be null, indicating the file was not loaded from disk.
            /// </summary>
            internal RegularElementLocation(string file, int line, int column)
            {
                ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(file, "file");
                ErrorUtilities.VerifyThrow(line > -1 && column > -1, "Use zero for unknown");

                this.file = file ?? String.Empty;
                this.line = line;
                this.column = column;
            }

            /// <summary>
            /// The file from which this particular element originated.  It may
            /// differ from the ProjectFile if, for instance, it was part of
            /// an import or originated in a targets file.
            /// If not known, returns empty string.
            /// </summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override string File
            {
                get { return file; }
            }

            /// <summary>
            /// The line number where this element exists in its file.
            /// The first line is numbered 1.
            /// Zero indicates "unknown location".
            /// </summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override int Line
            {
                get { return line; }
            }

            /// <summary>
            /// The column number where this element exists in its file.
            /// The first column is numbered 1.
            /// Zero indicates "unknown location".
            /// </summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override int Column
            {
                get { return column; }
            }
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
            /// The source file.
            /// </summary>
            private string file;

            /// <summary>
            /// The source line.
            /// </summary>
            private ushort line;

            /// <summary>
            /// The source column.
            /// </summary>
            private ushort column;

            /// <summary>
            /// Constructor for the case where we have most or all information.
            /// Numerical values must be 1-based, non-negative; 0 indicates unknown
            /// File may be null or empty, indicating the file was not loaded from disk.
            /// </summary>
            internal SmallElementLocation(string file, int line, int column)
            {
                ErrorUtilities.VerifyThrow(line > -1 && column > -1, "Use zero for unknown");
                ErrorUtilities.VerifyThrow(line <= 65535 && column <= 65535, "Use ElementLocation instead");

                this.file = file ?? String.Empty;
                this.line = Convert.ToUInt16(line);
                this.column = Convert.ToUInt16(column);
            }

            /// <summary>
            /// The file from which this particular element originated.  It may
            /// differ from the ProjectFile if, for instance, it was part of
            /// an import or originated in a targets file.
            /// If not known, returns empty string.
            /// </summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override string File
            {
                get { return file; }
            }

            /// <summary>
            /// The line number where this element exists in its file.
            /// The first line is numbered 1.
            /// Zero indicates "unknown location".
            /// </summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override int Line
            {
                get { return (int)line; }
            }

            /// <summary>
            /// The column number where this element exists in its file.
            /// The first column is numbered 1.
            /// Zero indicates "unknown location".
            /// </summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override int Column
            {
                get { return (int)column; }
            }
        }
    }
}
