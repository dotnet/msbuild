// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Xml.Schema;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class encapsulates information about a file that is associated with a build event.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class BuildEventFileInfo
    {
        #region Constructors

        /// <summary>
        /// Private default constructor disallows parameterless instantiation.
        /// </summary>
        /// <owner>SumedhK</owner>
        private BuildEventFileInfo()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class using the given filename/path.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="file"></param>
        internal BuildEventFileInfo(string file)
            : this(file, 0, 0, 0, 0)
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class using the given filename/path and a line/column of interest in the file.
        /// </summary>
        /// <owner>SumedhK</owner>
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
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="file"></param>
        /// <param name="line">Set to zero if not available.</param>
        /// <param name="column">Set to zero if not available.</param>
        /// <param name="endLine">Set to zero if not available.</param>
        /// <param name="endColumn">Set to zero if not available.</param>
        internal BuildEventFileInfo(string file, int line, int column, int endLine, int endColumn)
        {
            ErrorUtilities.VerifyThrow(file != null, "Need filename/path.");

            this.file = file;
            this.line = line;
            this.column = column;
            this.endLine = endLine;
            this.endColumn = endColumn;
        }

        /// <summary>
        /// Creates an instance of this class using the information in the given XmlException.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="e"></param>
        internal BuildEventFileInfo(XmlException e)
        {
            ErrorUtilities.VerifyThrow(e != null, "Need exception context.");

            this.file = (e.SourceUri.Length == 0) ? String.Empty : new Uri(e.SourceUri).LocalPath;
            this.line = e.LineNumber;
            this.column = e.LinePosition;
            this.endLine = 0;
            this.endColumn = 0;
        }

        /// <summary>
        /// Creates an instance of this class using the information in the given XmlSchemaException.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="e"></param>
        internal BuildEventFileInfo(XmlSchemaException e)
        {
            ErrorUtilities.VerifyThrow(e != null, "Need exception context.");

            this.file = (e.SourceUri.Length == 0) ? String.Empty : new Uri(e.SourceUri).LocalPath;
            this.line = e.LineNumber;
            this.column = e.LinePosition;
            this.endLine = 0;
            this.endColumn = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the filename/path to be associated with some build event.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The filename/path string.</value>
        internal string File
        {
            get
            {
                return file;
            }
        }

        /// <summary>
        /// Gets the line number of interest in the file.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Line number, or zero if not available.</value>
        internal int Line
        {
            get
            {
                return line;
            }
        }

        /// <summary>
        /// Gets the column number of interest in the file.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Column number, or zero if not available.</value>
        internal int Column
        {
            get
            {
                return column;
            }
        }

        /// <summary>
        /// Gets the last line number of a range of interesting lines in the file.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Last line number, or zero if not available.</value>
        internal int EndLine
        {
            get
            {
                return endLine;
            }
        }

        /// <summary>
        /// Gets the last column number of a range of interesting columns in the file.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Last column number, or zero if not available.</value>
        internal int EndColumn
        {
            get
            {
                return endColumn;
            }
        }

        #endregion

        // the filename/path
        private string file;
        // the line number of interest in the file
        private int line;
        // the column number of interest in the file
        private int column;
        // the last line in a range of interesting lines in the file
        private int endLine;
        // the last column in a range of interesting columns in the file
        private int endColumn;
    }
}
