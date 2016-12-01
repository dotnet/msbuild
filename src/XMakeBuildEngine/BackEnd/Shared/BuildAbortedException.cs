// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>The exception which gets thrown if the build is aborted gracefully.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif
using Microsoft.Build.Shared;

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
    public class BuildAbortedException : Exception
    {
        /// <summary>
        /// Constructs a standard BuildAbortedException.
        /// </summary>
        public BuildAbortedException()
            : base(ResourceUtilities.FormatResourceString("BuildAborted"))
        {
            string errorCode;
            string helpKeyword;

            ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "BuildAborted");

            this.ErrorCode = errorCode;
        }

        /// <summary>
        /// Constructs a BuildAbortedException with an additional message attached.
        /// </summary>
        public BuildAbortedException(string message)
            : base(ResourceUtilities.FormatResourceString("BuildAbortedWithMessage", message))
        {
            string errorCode;
            string helpKeyword;

            ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "BuildAbortedWithMessage", message);

            this.ErrorCode = errorCode;
        }

        /// <summary>
        /// Constructs a BuildAbortedException with an additional message attached and an inner exception.
        /// </summary>
        public BuildAbortedException(string message, Exception innerException)
            : base(ResourceUtilities.FormatResourceString("BuildAbortedWithMessage", message), innerException)
        {
            string errorCode;
            string helpKeyword;

            ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "BuildAbortedWithMessage", message);

            this.ErrorCode = errorCode;
        }

#if FEATURE_BINARY_SERIALIZATION
        /// <summary>
        /// Protected constructor used for (de)serialization. 
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        protected BuildAbortedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.ErrorCode = info.GetString("ErrorCode");
        }
#endif

        /// <summary>
        /// Gets the error code (if any) associated with the exception message.
        /// </summary>
        /// <value>Error code string, or null.</value>
        public string ErrorCode
        {
            get;
            private set;
        }

#if FEATURE_BINARY_SERIALIZATION
        /// <summary>
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
#if FEATURE_SECURITY_PERMISSIONS
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
#endif
        override public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("ErrorCode", ErrorCode);
        }
#endif
    }
}
