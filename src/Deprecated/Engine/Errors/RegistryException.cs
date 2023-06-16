// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Generic exception used to wrap exceptions thrown during Registry access.
    /// </summary>
    [Serializable]
    internal class RegistryException : Exception
    {
        /// <summary>
        /// Basic constructor.
        /// </summary>
        public RegistryException()
            : base()
        {
        }

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="message"></param>
        public RegistryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public RegistryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructor that takes a string description of the registry
        /// key or value causing the error.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="source"></param>
        public RegistryException(string message, string source)
            : base(message)
        {
            base.Source = source;
        }

        /// <summary>
        /// Since this class implements Iserializable this constructor is required to be implemented.
        /// </summary>
        protected RegistryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            // We don't have any reason at the moment to do any custom serizlization or deserialization, this methods was added
            // to conform to the implementation of the standard constructors for ISerializable classes
        }

        /// <summary>
        /// Constructor that takes a string description of the registry
        /// key or value causing the error.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="source"></param>
        /// <param name="innerException"></param>
        public RegistryException(string message, string source, Exception innerException)
            : base(message, innerException)
        {
            base.Source = source;
        }
    }
}
