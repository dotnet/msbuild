// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Runtime.Serialization;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Exceptions
{
    /// <summary>
    /// This exception is thrown whenever there is a problem with the user's XML project file. The problem might be semantic or
    /// syntactical. The latter would be of a type typically caught by XSD validation (if it was performed by the project writer).
    /// </summary>
    /// <remarks>
    /// WARNING: marking a type [Serializable] without implementing ISerializable imposes a serialization contract -- it is a
    /// promise to never change the type's fields i.e. the type is immutable; adding new fields in the next version of the type
    /// without following certain special FX guidelines, can break both forward and backward compatibility
    /// </remarks>
    [Serializable]
    public sealed class InvalidProjectFileException : Exception
    {
        #region Basic constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use a rich constructor whenever possible.
        /// </remarks>
        public InvalidProjectFileException()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this exception using the specified error message.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use a rich constructor whenever possible.
        /// </remarks>
        /// <param name="message"></param>
        public InvalidProjectFileException(string message)
            : base(message)
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this exception using the specified error message and inner exception.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use a rich constructor whenever possible.
        /// </remarks>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public InvalidProjectFileException(string message, Exception innerException)
            : base(message, innerException)
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this exception using the specified error message and inner invalid project file exception.
        /// This is used in order to wrap and exception rather than rethrow it verbatim, which would reset the callstack.
        /// The assumption is that all the metadata for the outer exception comes from the inner exception, eg., they have the same error code.
        /// </summary>
        internal InvalidProjectFileException(string message, InvalidProjectFileException innerException)
            : this(innerException.ProjectFile, innerException.LineNumber, innerException.ColumnNumber, innerException.EndLineNumber, innerException.EndColumnNumber, message, innerException.ErrorSubcategory, innerException.ErrorCode, innerException.HelpKeyword)
        {
        }

        #endregion

        #region Serialization (update when adding new class members)

        /// <summary>
        /// Protected constructor used for (de)serialization. 
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        private InvalidProjectFileException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            file = info.GetString("projectFile");
            lineNumber = info.GetInt32("lineNumber");
            columnNumber = info.GetInt32("columnNumber");
            endLineNumber = info.GetInt32("endLineNumber");
            endColumnNumber = info.GetInt32("endColumnNumber");
            errorSubcategory = info.GetString("errorSubcategory");
            errorCode = info.GetString("errorCode");
            helpKeyword = info.GetString("helpKeyword");
            hasBeenLogged = info.GetBoolean("hasBeenLogged");
        }

        /// <summary>
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
#if FEATURE_SECURITY_PERMISSIONS
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
#endif
        override public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("projectFile", file);
            info.AddValue("lineNumber", lineNumber);
            info.AddValue("columnNumber", columnNumber);
            info.AddValue("endLineNumber", endLineNumber);
            info.AddValue("endColumnNumber", endColumnNumber);
            info.AddValue("errorSubcategory", errorSubcategory);
            info.AddValue("errorCode", errorCode);
            info.AddValue("helpKeyword", helpKeyword);
            info.AddValue("hasBeenLogged", hasBeenLogged);
        }

        #endregion

        #region Rich constructors

        /// <summary>
        /// Creates an instance of this exception using rich error information.
        /// </summary>
        /// <remarks>This constructor is preferred over the basic constructors.</remarks>
        /// <param name="projectFile">The invalid project file (can be empty string).</param>
        /// <param name="lineNumber">The invalid line number in the project (set to zero if not available).</param>
        /// <param name="columnNumber">The invalid column number in the project (set to zero if not available).</param>
        /// <param name="endLineNumber">The end of a range of invalid lines in the project (set to zero if not available).</param>
        /// <param name="endColumnNumber">The end of a range of invalid columns in the project (set to zero if not available).</param>
        /// <param name="message">Error message for exception.</param>
        /// <param name="errorSubcategory">Error sub-category that describes the error (can be null).</param>
        /// <param name="errorCode">The error code (can be null).</param>
        /// <param name="helpKeyword">The F1-help keyword for the host IDE (can be null).</param>
        public InvalidProjectFileException
        (
            string projectFile,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            string errorSubcategory,
            string errorCode,
            string helpKeyword
        ) :
            this(projectFile, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, errorSubcategory, errorCode, helpKeyword, null)
        {
        }

        /// <summary>
        /// Creates an instance of this exception using rich error information.
        /// </summary>
        /// <remarks>This constructor is preferred over the basic constructors.</remarks>
        /// <param name="projectFile">The invalid project file (can be empty string).</param>
        /// <param name="lineNumber">The invalid line number in the project (set to zero if not available).</param>
        /// <param name="columnNumber">The invalid column number in the project (set to zero if not available).</param>
        /// <param name="endLineNumber">The end of a range of invalid lines in the project (set to zero if not available).</param>
        /// <param name="endColumnNumber">The end of a range of invalid columns in the project (set to zero if not available).</param>
        /// <param name="message">Error message for exception.</param>
        /// <param name="errorSubcategory">Error sub-category that describes the error (can be null).</param>
        /// <param name="errorCode">The error code (can be null).</param>
        /// <param name="helpKeyword">The F1-help keyword for the host IDE (can be null).</param>
        /// <param name="innerException">Any inner exception. May be null.</param>
        internal InvalidProjectFileException
        (
            string projectFile,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            string errorSubcategory,
            string errorCode,
            string helpKeyword,
            Exception innerException
        ) : base(message, innerException)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFile, nameof(projectFile));
            ErrorUtilities.VerifyThrowArgumentLength(message, nameof(message));

            // Try to helpfully provide a full path if possible, but do so robustly.
            // This exception might be because the path was invalid!
            // Also don't consider "MSBUILD" a path: that's what msbuild.exe uses when there's no project associated.
            if (projectFile.Length > 0 && !String.Equals(projectFile, "MSBUILD", StringComparison.Ordinal))
            {
                string fullPath = FileUtilities.GetFullPathNoThrow(projectFile);

                projectFile = fullPath ?? projectFile;
            }

            file = projectFile;
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
            this.endLineNumber = endLineNumber;
            this.endColumnNumber = endColumnNumber;
            this.errorSubcategory = errorSubcategory;
            this.errorCode = errorCode;
            this.helpKeyword = helpKeyword;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the exception message including the affected project file (if any).
        /// </summary>
        /// <value>The complete message string.</value>
        public override string Message
        {
            get
            {
                return base.Message + ((!String.IsNullOrEmpty(ProjectFile))
                    ? ("  " + ProjectFile)
                    : null);
            }
        }

        /// <summary>
        /// Gets the exception message not including the project file.
        /// </summary>
        /// <value>The error message string only.</value>
        public string BaseMessage
        {
            get
            {
                return base.Message;
            }
        }

        /// <summary>
        /// Gets the file (if any) associated with this exception.
        /// This may be an imported file.
        /// </summary>
        /// <remarks>
        /// The name is poorly chosen as this may be a targets file,
        /// but the name has shipped now.
        /// </remarks>
        /// <value>Project filename/path string, or null.</value>
        public string ProjectFile
        {
            get
            {
                return file;
            }
        }

        /// <summary>
        /// Gets the invalid line number (if any) in the project.
        /// </summary>
        /// <value>The invalid line number, or zero.</value>
        public int LineNumber
        {
            get
            {
                return lineNumber;
            }
        }

        /// <summary>
        /// Gets the invalid column number (if any) in the project.
        /// </summary>
        /// <value>The invalid column number, or zero.</value>
        public int ColumnNumber
        {
            get
            {
                return columnNumber;
            }
        }

        /// <summary>
        /// Gets the last line number (if any) of a range of invalid lines in the project.
        /// </summary>
        /// <value>The last invalid line number, or zero.</value>
        public int EndLineNumber
        {
            get
            {
                return endLineNumber;
            }
        }

        /// <summary>
        /// Gets the last column number (if any) of a range of invalid columns in the project.
        /// </summary>
        /// <value>The last invalid column number, or zero.</value>
        public int EndColumnNumber
        {
            get
            {
                return endColumnNumber;
            }
        }

        /// <summary>
        /// Gets the error sub-category (if any) that describes the type of this error.
        /// </summary>
        /// <value>The sub-category string, or null.</value>
        public string ErrorSubcategory
        {
            get
            {
                return errorSubcategory;
            }
        }

        /// <summary>
        /// Gets the error code (if any) associated with the exception message.
        /// </summary>
        /// <value>Error code string, or null.</value>
        public string ErrorCode
        {
            get
            {
                return errorCode;
            }
        }

        /// <summary>
        /// Gets the F1-help keyword (if any) associated with this error, for the host IDE.
        /// </summary>
        /// <value>The keyword string, or null.</value>
        public string HelpKeyword
        {
            get
            {
                return helpKeyword;
            }
        }

        /// <summary>
        /// Whether the exception has already been logged. Allows the exception to be logged at the 
        /// most appropriate location, but continue to be propagated.
        /// </summary>
        public bool HasBeenLogged
        {
            get
            {
                return hasBeenLogged;
            }
            internal set
            {
                hasBeenLogged = value;
            }
        }

        #endregion

        // the project file that caused this exception
        private string file;
        // the invalid line number in the project
        private int lineNumber;
        // the invalid column number in the project
        private int columnNumber;
        // the end of a range of invalid lines in the project
        private int endLineNumber;
        // the end of a range of invalid columns in the project
        private int endColumnNumber;
        // the error sub-category that describes the type of this error
        private string errorSubcategory;
        // the error code for the exception message
        private string errorCode;
        // the F1-help keyword for the host IDE
        private string helpKeyword;
        // Has this errors been sent to the loggers?
        private bool hasBeenLogged = false;
    }
}
