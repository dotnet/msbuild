// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Runtime.Serialization;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Exceptions
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
