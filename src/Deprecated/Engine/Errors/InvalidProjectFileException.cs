// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Xml;
using System.Runtime.Serialization;
using System.Security.Permissions;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
    /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
    /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
    /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
    /// 
    /// This exception is thrown whenever there is a problem with the user's XML project file. The problem might be semantic or
    /// syntactical. The latter would be of a type typically caught by XSD validation (if it was performed by the project writer).
    /// </summary>
    /// <remarks>
    /// <format type="text/markdown"><![CDATA[
    /// ## Remarks
    /// > [!WARNING]
    /// > This class (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
    /// > <xref:Microsoft.Build.Construction>
    /// > <xref:Microsoft.Build.Evaluation>
    /// > <xref:Microsoft.Build.Execution>
    /// ]]></format>
    /// </remarks>
    /// <owner>RGoel</owner>
    // WARNING: marking a type [Serializable] without implementing ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both forward and backward compatibility

    [Serializable]
    public sealed class InvalidProjectFileException : Exception
    {
        #region Basic constructors

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Default constructor.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use a rich constructor whenever possible.
        /// </remarks>
        /// <owner>RGoel</owner>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public InvalidProjectFileException()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Creates an instance of this exception using the specified error message.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use a rich constructor whenever possible.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="message"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public InvalidProjectFileException(string message)
            : base(message)
        {
            // do nothing
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Creates an instance of this exception using the specified error message and inner exception.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use a rich constructor whenever possible.
        /// </remarks>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Creates an instance of this exception using rich error information.
        /// </summary>
        /// <remarks>This constructor is preferred over the basic constructors.</remarks>
        /// <owner>RGoel, SumedhK</owner>
        /// <param name="xmlNode">The XML node where the error is (can be null).</param>
        /// <param name="message">Error message for exception.</param>
        /// <param name="errorSubcategory">Error sub-category that describes the error (can be null).</param>
        /// <param name="errorCode">The error code (can be null).</param>
        /// <param name="helpKeyword">The F1-help keyword for the host IDE (can be null).</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
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
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Gets the exception message including the affected project file (if any).
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The complete message string.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Gets the exception message not including the project file.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The error message string only.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public string BaseMessage
        {
            get
            {
                return base.Message;
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Gets the project file (if any) associated with this exception.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Project filename/path string, or null.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public string ProjectFile
        {
            get
            {
                return projectFile;
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Gets the invalid line number (if any) in the project.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The invalid line number, or zero.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public int LineNumber
        {
            get
            {
                return lineNumber;
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Gets the invalid column number (if any) in the project.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The invalid column number, or zero.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public int ColumnNumber
        {
            get
            {
                return columnNumber;
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Gets the last line number (if any) of a range of invalid lines in the project.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The last invalid line number, or zero.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public int EndLineNumber
        {
            get
            {
                return endLineNumber;
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Gets the last column number (if any) of a range of invalid columns in the project.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The last invalid column number, or zero.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public int EndColumnNumber
        {
            get
            {
                return endColumnNumber;
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Gets the error sub-category (if any) that describes the type of this error.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The sub-category string, or null.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public string ErrorSubcategory
        {
            get
            {
                return errorSubcategory;
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Gets the error code (if any) associated with the exception message.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Error code string, or null.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public string ErrorCode
        {
            get
            {
                return errorCode;
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Gets the F1-help keyword (if any) associated with this error, for the host IDE.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The keyword string, or null.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
