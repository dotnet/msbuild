// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    /// This exception is used to wrap an unhandled exception from a project cache plugin. This exception aborts the build, and it can only be
    /// thrown by the MSBuild engine.
    /// </summary>
    public sealed class ProjectCacheException : Exception
    {
        private ProjectCacheException()
        {
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        private ProjectCacheException(
            string message,
            Exception innerException,
            bool hasBeenLogged,
            string errorCode
        )
            : base(message, innerException)
        {
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(message), "Need error message.");
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(errorCode), "Must specify the error message code.");

            HasBeenLogged = hasBeenLogged;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// The project cache has already logged this as an error.
        /// Should not get logged again.
        /// </summary>
        public bool HasBeenLogged { get; }

        /// <summary>
        /// Gets the error code associated with this exception's message (not the inner exception).
        /// </summary>
        /// <value>The error code string.</value>
        public string ErrorCode { get; }

        internal static void ThrowAsUnhandledException
        (
            Exception innerException,
            string messageResourceName,
            params string[] messageArgs
        )
        {
            ErrorUtilities.VerifyThrow(messageResourceName != null, "Need error message.");

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out var errorCode, out _, messageResourceName, messageArgs);

            throw new ProjectCacheException(message, innerException, hasBeenLogged: false, errorCode);
        }

        internal static void ThrowForLoggedError
        (
            string messageResourceName,
            params string[] messageArgs
        )
        {
            ErrorUtilities.VerifyThrow(messageResourceName != null, "Need error message.");

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out var errorCode, out _, messageResourceName, messageArgs);

            throw new ProjectCacheException(message: message, innerException: null, hasBeenLogged: true, errorCode: errorCode);
        }
    }
}
