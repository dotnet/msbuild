// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This exception is to be thrown whenever an assumption we have made in the code turns out to be false. Thus, if this
    /// exception ever gets thrown, it is because of a bug in our own code, not because of something the user or project author
    /// did wrong.
    /// 
    /// !~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~
    /// WARNING: When this file is shared into multiple assemblies each assembly will view this as a different type.
    ///          Don't throw this exception from one assembly and catch it in another.
    /// !~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~
    ///     
    /// </summary>
    /// <owner>RGoel</owner>
    [Serializable]
    internal sealed class InternalErrorException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <owner>RGoel</owner>
        internal InternalErrorException() : base()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this exception using the given message.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="message"></param>
        internal InternalErrorException
        (
            String message
        ) :
            base("Internal MSBuild Error: " + message)
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this exception using the given message and inner exception.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        internal InternalErrorException
        (
            String message,
            Exception innerException
        ) :
            base("Internal MSBuild Error: " + message, innerException)
        {
            // do nothing
        }

        #region Serialization (update when adding new class members)

        /// <summary>
        /// Private constructor used for (de)serialization. The constructor is private as this class is sealed
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        private InternalErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            // Do nothing: no fields
        }

        // Base implementation of GetObjectData() is sufficient; we have no fields

        #endregion
    }
}
