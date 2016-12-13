// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Xml.Schema;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class encapsulates information about a file that is associated with a build event.
    /// </summary>
    internal sealed class BuildEventFileInfo
    {
        #region Constructors

        /// <summary>
        /// Private default constructor disallows parameterless instantiation.
        /// </summary>
        private BuildEventFileInfo()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class using the given filename/path.
        /// Filename may be an empty string, if there is truly no file associated.
        /// This overload may also be used if there is a file but truly no line/column,
        /// for example when failing to load a project file.
        /// 
        /// IF AN IELEMENTLOCATION IS AVAILABLE, USE THE OVERLOAD ACCEPTING THAT INSTEAD.
        /// </summary>
        /// <param name="file"></param>
        internal BuildEventFileInfo(string file)
            : this(file, 0, 0, 0, 0)
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class using the given location.
        /// This does not provide end-line or end-column information.
        /// This is the preferred overload.
        /// </summary>
        internal BuildEventFileInfo(IElementLocation location)
            : this(location.File, location.Line, location.Column)
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class using the given filename/path and a line/column of interest in the file.
        /// 
        /// IF AN IELEMENTLOCATION IS AVAILABLE, USE THE OVERLOAD ACCEPTING THAT INSTEAD.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="line">Set to zero if not available.</param>
        /// <param name="column">Set to zero if not available.</param>
        internal BuildEventFileInfo(string file, int line, int column)
            : this(file, line, column, 0, 0)
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class using the given filename/path and a range of lines/columns of interest in the file.
        /// 
        /// IF AN IELEMENTLOCATION IS AVAILABLE, USE THE OVERLOAD ACCEPTING THAT INSTEAD.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="line">Set to zero if not available.</param>
        /// <param name="column">Set to zero if not available.</param>
        /// <param name="endLine">Set to zero if not available.</param>
        /// <param name="endColumn">Set to zero if not available.</param>
        internal BuildEventFileInfo(string file, int line, int column, int endLine, int endColumn)
        {
            // Projects that don't have a filename when the are built should use an empty string instead.
            _file = (file == null) ? String.Empty : file;
            _line = line;
            _column = column;
            _endLine = endLine;
            _endColumn = endColumn;
        }

        /// <summary>
        /// Creates an instance of this class using the information in the given XmlException.
        /// </summary>
        /// <param name="e"></param>
        internal BuildEventFileInfo(XmlException e)
        {
            ErrorUtilities.VerifyThrow(e != null, "Need exception context.");
#if FEATURE_XML_SOURCE_URI
            _file = (e.SourceUri.Length == 0) ? String.Empty : new Uri(e.SourceUri).LocalPath;
#else
            _file = String.Empty;
#endif
            _line = e.LineNumber;
            _column = e.LinePosition;
            _endLine = 0;
            _endColumn = 0;
        }

        /// <summary>
        /// Creates an instance of this class using the information in the given XmlException and file location.
        /// </summary>
        internal BuildEventFileInfo(string file, XmlException e) : this(e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(file, nameof(file));

            _file = file;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the filename/path to be associated with some build event.
        /// </summary>
        /// <value>The filename/path string.</value>
        internal string File
        {
            get
            {
                return _file;
            }
        }

        /// <summary>
        /// Gets the line number of interest in the file.
        /// </summary>
        /// <value>Line number, or zero if not available.</value>
        internal int Line
        {
            get
            {
                return _line;
            }
        }

        /// <summary>
        /// Gets the column number of interest in the file.
        /// </summary>
        /// <value>Column number, or zero if not available.</value>
        internal int Column
        {
            get
            {
                return _column;
            }
        }

        /// <summary>
        /// Gets the last line number of a range of interesting lines in the file.
        /// </summary>
        /// <value>Last line number, or zero if not available.</value>
        internal int EndLine
        {
            get
            {
                return _endLine;
            }
        }

        /// <summary>
        /// Gets the last column number of a range of interesting columns in the file.
        /// </summary>
        /// <value>Last column number, or zero if not available.</value>
        internal int EndColumn
        {
            get
            {
                return _endColumn;
            }
        }

        #endregion

        // the filename/path
        private string _file;
        // the line number of interest in the file
        private int _line;
        // the column number of interest in the file
        private int _column;
        // the last line in a range of interesting lines in the file
        private int _endLine;
        // the last column in a range of interesting columns in the file
        private int _endColumn;
    }
}
