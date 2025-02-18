// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework.BuildException;

#nullable disable

namespace Microsoft.Build.Execution
{
    [Serializable]
    internal sealed class HostObjectException : BuildExceptionBase
    {
        private const string ErrorMessagePrefix = "Error for HostObject:";
        private const string ErrorMessageProjectTargetTask = "In Project '{0}', Target '{1}', Task '{2}'.";

        internal HostObjectException() : base()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this exception using the given message.
        /// </summary>
        internal HostObjectException(
            string message) :
            base(ErrorMessagePrefix + message)
        {
        }

        /// <summary>
        /// Creates an instance of this exception using projectFile, targetName, taskName and inner exception.
        /// Adds the inner exception's details to the exception message because most bug reporters don't bother
        /// to provide the inner exception details which is typically what we care about.
        /// </summary>
        internal HostObjectException(
            string projectFile,
            string targetName,
            string taskName,
            Exception innerException) :
            base(ErrorMessagePrefix
                + string.Format(ErrorMessageProjectTargetTask, projectFile, targetName, taskName)
                + (innerException == null ? string.Empty : ($"\n=============\n{innerException}\n\n")),
                innerException)
        {
        }

        /// <summary>
        /// Creates an instance of this exception using projectFile, targetName, taskName and message.
        /// </summary>
        internal HostObjectException(
            string projectFile,
            string targetName,
            string taskName,
            string message) :
            base(ErrorMessagePrefix
                + string.Format(ErrorMessageProjectTargetTask, projectFile, targetName, taskName) + message)
        {
        }

        internal HostObjectException(string message, Exception innerException)
            : base(
                message,
                innerException)
        { }
    }
}
