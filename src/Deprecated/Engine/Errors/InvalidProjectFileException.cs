// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Runtime.Serialization;
using System.Security.Permissions;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
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
    /// <owner>RGoel</owner>
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
        /// <owner>RGoel</owner>
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
        /// <owner>SumedhK</owner>
        /// <param name="message"></param>
        public InvalidProjectFileException(string message)
            : base(message)
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this exception using the specified error message and inner exception.
        /// </summary>
        /// <owner>SumedhK</owner>
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
            this.projectFile = info.GetString("projectFile");
            this.lineNumber = info.GetInt32("lineNumber");
            this.columnNumber = info.GetInt32("columnNumber");
            this.endLineNumber = info.GetInt32("endLineNumber");
            this.endColumnNumber = info.GetInt32("endColumnNumber");
            this.errorSubcategory = info.GetString("errorSubcategory");
            this.errorCode = info.GetString("errorCode");
            this.helpKeyword = info.GetString("helpKeyword");
            this.hasBeenLogged = info.GetBoolean("hasBeenLogged");
        }

        /// <summary>
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        override public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("projectFile", projectFile);
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
        /// <owner>RGoel, SumedhK</owner>
        /// <param name="xmlNode">The XML node where the error is (can be null).</param>
        /// <param name="message">Error message for exception.</param>
        /// <param name="errorSubcategory">Error sub-category that describes the error (can be null).</param>
        /// <param name="errorCode">The error code (can be null).</param>
        /// <param name="helpKeyword">The F1-help keyword for the host IDE (can be null).</param>
        public InvalidProjectFileException
        (
            XmlNode xmlNode,
            string message,
            string errorSubcategory,
            string errorCode,
            string helpKeyword
        ) : 
            base(message)
        {
            ErrorUtilities.VerifyThrowArgumentLength(message, nameof(message));

            if (xmlNode != null)
            {
                this.projectFile = XmlUtilities.GetXmlNodeFile(xmlNode, String.Empty /* no project file if XML is purely in-memory */);
                XmlSearcher.GetLineColumnByNode(xmlNode, out this.lineNumber, out this.columnNumber);
            }

            this.errorSubcategory = errorSubcategory;
            this.errorCode = errorCode;
            this.helpKeyword = helpKeyword;
        }

        /// <summary>
        /// Creates an instance of this exception using rich error information.
        /// </summary>
        /// <remarks>This constructor is preferred over the basic constructors.</remarks>
        /// <owner>SumedhK</owner>
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
            base(message)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFile, nameof(projectFile));
            ErrorUtilities.VerifyThrowArgumentLength(message, nameof(message));

            this.projectFile = projectFile;
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
        /// <owner>SumedhK</owner>
        /// <value>The complete message string.</value>
        public override string Message
        {
            get
            {
                return base.Message + ((ProjectFile != null)
                    ? ("  " + ProjectFile)
                    : null);
            }
        }

        /// <summary>
        /// Gets the exception message not including the project file.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The error message string only.</value>
        public string BaseMessage
        {
            get
            {
                return base.Message;
            }
        }

        /// <summary>
        /// Gets the project file (if any) associated with this exception.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Project filename/path string, or null.</value>
        public string ProjectFile
        {
            get
            {
                return projectFile;
            }
        }

        /// <summary>
        /// Gets the invalid line number (if any) in the project.
        /// </summary>
        /// <owner>SumedhK</owner>
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
        /// <owner>SumedhK</owner>
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
        /// <owner>SumedhK</owner>
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
        /// <owner>SumedhK</owner>
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
        /// <owner>SumedhK</owner>
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
        /// <owner>SumedhK</owner>
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
        /// <owner>SumedhK</owner>
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
        internal bool HasBeenLogged
        {
            get
            {
                return this.hasBeenLogged;
            }
            set
            {
                this.hasBeenLogged = value;
            }
        }

        #endregion

        // the project file that caused this exception
        private string projectFile;
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
