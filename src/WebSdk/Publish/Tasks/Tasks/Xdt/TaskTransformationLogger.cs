// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Web.XmlTransform;

namespace Microsoft.NET.Sdk.Publish.Tasks.Xdt
{
    internal class TaskTransformationLogger : IXmlTransformationLogger
    {
        #region private data members
        private TaskLoggingHelper loggingHelper;

        private int indentLevel = 0;
        private readonly string indentStringPiece = "  ";
        private string indentString = null;

        private bool stackTrace;
        #endregion

        public TaskTransformationLogger(TaskLoggingHelper loggingHelper)
            : this(loggingHelper, false)
        {
        }

        public TaskTransformationLogger(TaskLoggingHelper loggingHelper, bool stackTrace)
        {
            this.loggingHelper = loggingHelper;
            this.stackTrace = stackTrace;
        }

        private string IndentString
        {
            get
            {
                if (indentString == null)
                {
                    indentString = String.Empty;
                    for (int i = 0; i < indentLevel; i++)
                    {
                        indentString += indentStringPiece;
                    }
                }
                return indentString;
            }
        }

        private int IndentLevel
        {
            get
            {
                return indentLevel;
            }
            set
            {
                if (indentLevel != value)
                {
                    indentLevel = value;
                    indentString = null;
                }
            }
        }

        #region IXmlTransformationLogger Members
        void IXmlTransformationLogger.LogMessage(string message, params object[] messageArgs)
        {
            ((IXmlTransformationLogger)this).LogMessage(MessageType.Normal, message, messageArgs);
        }

        void IXmlTransformationLogger.LogMessage(MessageType type, string message, params object[] messageArgs)
        {
            MessageImportance importance;
            switch (type)
            {
                case MessageType.Normal:
                    importance = MessageImportance.Normal;
                    break;
                case MessageType.Verbose:
                    importance = MessageImportance.Low;
                    break;
                default:
                    Debug.Fail("Unknown MessageType");
                    importance = MessageImportance.Normal;
                    break;
            }

            loggingHelper.LogMessage(importance, String.Concat(IndentString, message), messageArgs);
        }

        void IXmlTransformationLogger.LogWarning(string message, params object[] messageArgs)
        {
            loggingHelper.LogWarning(message, messageArgs);
        }

        void IXmlTransformationLogger.LogWarning(string file, string message, params object[] messageArgs)
        {
            ((IXmlTransformationLogger)this).LogWarning(file, 0, 0, message, messageArgs);
        }

        void IXmlTransformationLogger.LogWarning(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            loggingHelper.LogWarning(
                null,
                null,
                null,
                file,
                lineNumber,
                linePosition,
                0,
                0,
                loggingHelper.FormatString(message, messageArgs));
        }

        void IXmlTransformationLogger.LogError(string message, params object[] messageArgs)
        {
            loggingHelper.LogError(message, messageArgs);
        }

        void IXmlTransformationLogger.LogError(string file, string message, params object[] messageArgs)
        {
            ((IXmlTransformationLogger)this).LogError(file, 0, 0, message, messageArgs);
        }

        void IXmlTransformationLogger.LogError(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            loggingHelper.LogError(
                null,
                null,
                null,
                file,
                lineNumber,
                linePosition,
                0,
                0,
                loggingHelper.FormatString(message, messageArgs));
        }

        void IXmlTransformationLogger.LogErrorFromException(Exception ex)
        {
            loggingHelper.LogErrorFromException(ex, stackTrace, stackTrace, null);
        }

        void IXmlTransformationLogger.LogErrorFromException(Exception ex, string file)
        {
            loggingHelper.LogErrorFromException(ex, stackTrace, stackTrace, file);
        }

        void IXmlTransformationLogger.LogErrorFromException(Exception ex, string file, int lineNumber, int linePosition)
        {
            string message = ex.Message;
            if (stackTrace)
            {
                // loggingHelper.LogErrorFromException does not have an overload
                // that accepts line numbers. So instead, we have to construct
                // the error message from the exception and use LogError.
                StringBuilder sb = new StringBuilder();
                Exception exIterator = ex;
                while (exIterator != null)
                {
                    sb.AppendFormat("{0} : {1}", exIterator.GetType().Name, exIterator.Message);
                    sb.AppendLine();
                    if (!String.IsNullOrEmpty(exIterator.StackTrace))
                    {
                        sb.Append(exIterator.StackTrace);
                    }
                    exIterator = exIterator.InnerException;
                }

                message = sb.ToString();
            }

            ((IXmlTransformationLogger)this).LogError(file, lineNumber, linePosition, message);
        }

        void IXmlTransformationLogger.StartSection(string message, params object[] messageArgs)
        {
            ((IXmlTransformationLogger)this).StartSection(MessageType.Normal, message, messageArgs);
        }

        void IXmlTransformationLogger.StartSection(MessageType type, string message, params object[] messageArgs)
        {
            ((IXmlTransformationLogger)this).LogMessage(type, message, messageArgs);
            IndentLevel++;
        }

        void IXmlTransformationLogger.EndSection(string message, params object[] messageArgs)
        {
            ((IXmlTransformationLogger)this).EndSection(MessageType.Normal, message, messageArgs);
        }

        void IXmlTransformationLogger.EndSection(MessageType type, string message, params object[] messageArgs)
        {
            Debug.Assert(IndentLevel > 0);
            if (IndentLevel > 0)
            {
                IndentLevel--;
            }

            ((IXmlTransformationLogger)this).LogMessage(type, message, messageArgs);
        }
        #endregion
    }
}
