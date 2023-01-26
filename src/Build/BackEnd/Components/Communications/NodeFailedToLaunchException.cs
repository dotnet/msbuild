// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// An exception representing the case where a TaskHost node failed to launch.
    /// This may happen for example when the TaskHost binary is corrupted.
    /// </summary>
    /// <remarks>
    /// If you add fields to this class, add a custom serialization constructor and override GetObjectData().
    /// </remarks>
    [Serializable]
    internal class NodeFailedToLaunchException : Exception
    {
        /// <summary>
        /// Constructs a standard NodeFailedToLaunchException.
        /// </summary>
        internal NodeFailedToLaunchException()
            : base()
        {
        }

        internal NodeFailedToLaunchException(Exception innerException)
            : base(innerException.Message, innerException)
        { }

        /// <summary>
        /// Constructs a standard NodeFailedToLaunchException.
        /// </summary>
        internal NodeFailedToLaunchException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
            ErrorDescription = message;
        }

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        protected NodeFailedToLaunchException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Gets the error code (if any) associated with the exception message.
        /// </summary>
        /// <value>Error code string, or null.</value>
        public string ErrorCode
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the error code (if any) associated with the exception message.
        /// </summary>
        /// <value>Error code string, or null.</value>
        public string ErrorDescription
        {
            get;
            private set;
        }

        /// <summary>
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
#if FEATURE_SECURITY_PERMISSIONS
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
#endif
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("ErrorCode", ErrorCode);
            info.AddValue("ErrorDescription", ErrorDescription);
        }
    }
}
