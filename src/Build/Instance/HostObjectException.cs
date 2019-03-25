// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Execution
{
    [Serializable]
    internal sealed class HostObjectException : Exception
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
        internal HostObjectException
        (
            string message
        ) :
            base(ErrorMessagePrefix + message)
        {
        }

        /// <summary>
        /// Creates an instance of this exception using projectFile, targetName, taskName and inner exception.
        /// Adds the inner exception's details to the exception message because most bug reporters don't bother
        /// to provide the inner exception details which is typically what we care about.
        /// </summary>
        internal HostObjectException
        (
            string projectFile,
            string targetName,
            string taskName,
            Exception innerException
        ) :
            base(ErrorMessagePrefix
                + string.Format(ErrorMessageProjectTargetTask, projectFile, targetName, taskName)
                + (innerException == null ? string.Empty : ("\n=============\n" + innerException.ToString() + "\n\n")),
                innerException)
        {
        }

        /// <summary>
        /// Creates an instance of this exception using projectFile, targetName, taskName and message.
        /// </summary>
        internal HostObjectException
        (
            string projectFile,
            string targetName,
            string taskName,
            string message
        ) :
            base(ErrorMessagePrefix
                + string.Format(ErrorMessageProjectTargetTask, projectFile, targetName, taskName) + message)
        {
        }
    }
}
