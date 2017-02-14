// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used to wrap exceptions that occur on a different node
    /// </summary>
    [Serializable]
    public sealed class RemoteErrorException : Exception
    {
        internal RemoteErrorException(string message, Exception innerException, BuildEventContext buildEventContext)
            : base(message, innerException)
        {
            ErrorUtilities.VerifyThrow((message != null) && (message.Length > 0), "Need error message.");
            ErrorUtilities.VerifyThrow(innerException != null, "Need the logger exception.");

            this.buildEventContext = buildEventContext;
        }

        #region Serialization (update when adding new class members)

        /// <summary>
        /// Protected constructor used for (de)serialization. 
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        private RemoteErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.buildEventContext = (BuildEventContext)info.GetValue("buildEventContext", typeof(BuildEventContext));
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

            info.AddValue("buildEventContext", buildEventContext);
        }

        #endregion

        #region Properties
        /// <summary>
        /// Gets the event context in which the remote exception occured
        /// </summary>
        internal BuildEventContext BuildEventContext
        {
            get
            {
                return buildEventContext;
            }
        }
        #endregion

        #region Methods
        internal static void Throw(Exception innerException, BuildEventContext buildEventContext, string messageResourceName, params string[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(messageResourceName != null, "Need error message.");

            string message = ResourceUtilities.FormatResourceString(messageResourceName, messageArgs);

            throw new RemoteErrorException(message, innerException, buildEventContext);
        }
        #endregion

        #region Data
        private BuildEventContext buildEventContext;
        #endregion
    }
}
