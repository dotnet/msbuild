// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.Build.Framework.BuildException;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Exceptions
{
    /// <summary>
    /// An exception representing the case where the build was aborted by request, as opposed to being
    /// unceremoniously shut down due to another kind of error exception.
    /// </summary>
    /// <remarks>
    /// This is public because it may be returned in the Exceptions collection of a BuildResult.
    /// If you add fields to this class, add a custom serialization constructor and override GetObjectData().
    /// </remarks>
    [Serializable]
    public class BuildAbortedException : BuildExceptionBase
    {
        /// <summary>
        /// Constructs a standard BuildAbortedException.
        /// </summary>
        public BuildAbortedException()
            : base(ResourceUtilities.GetResourceString("BuildAborted"))
        {
            ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "BuildAborted");

            ErrorCode = errorCode;
        }

        /// <summary>
        /// Constructs a BuildAbortedException with an additional message attached.
        /// </summary>
        public BuildAbortedException(string message)
            : base(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("BuildAbortedWithMessage", message))
        {
            ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "BuildAbortedWithMessage", message);

            ErrorCode = errorCode;
        }

        /// <summary>
        /// Constructs a BuildAbortedException with an additional message attached and an inner exception.
        /// </summary>
        public BuildAbortedException(string message, Exception innerException)
            : this(message, innerException, false)
        { }

        internal static BuildAbortedException CreateFromRemote(string message, Exception innerException)
        {
            return new BuildAbortedException(message, innerException, true /* calledFromDeserialization */);
        }

        private BuildAbortedException(string message, Exception innerException, bool calledFromDeserialization)
            : base(
                calledFromDeserialization
                    ? message
                    : ResourceUtilities.FormatResourceStringStripCodeAndKeyword("BuildAbortedWithMessage", message),
                innerException)
        {
            if (!calledFromDeserialization)
            {
                ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "BuildAbortedWithMessage", message);

                ErrorCode = errorCode;
            }
        }

        protected override IDictionary<string, string> FlushCustomState()
        {
            return new Dictionary<string, string>()
            {
                { nameof(ErrorCode), ErrorCode }
            };
        }

        protected override void InitializeCustomState(IDictionary<string, string> state)
        {
            ErrorCode = state[nameof(ErrorCode)];
        }

        /// <summary>
        /// Protected constructor used for (de)serialization.
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        protected BuildAbortedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorCode = info.GetString("ErrorCode");
        }

        /// <summary>
        /// Gets the error code (if any) associated with the exception message.
        /// </summary>
        /// <value>Error code string, or null.</value>
        public string ErrorCode { get; private set; }

        /// <summary>
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
#if FEATURE_SECURITY_PERMISSIONS
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
#endif
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("ErrorCode", ErrorCode);
        }
    }
}
