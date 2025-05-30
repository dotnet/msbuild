// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Internal exception thrown when there's an unrecoverable failure extracting public/private keys.
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both forward and backward compatibility
    [Serializable]
    internal class StrongNameException : Exception
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        internal StrongNameException()
        {
            // do nothing
        }

        /// <summary>
        /// Constructor that allows to preserve the original exception information
        /// </summary>
        internal StrongNameException(Exception innerException) : base("", innerException)
        {
            // do nothing
        }


        /// <summary>
        /// Constructor to implement required constructors for serialization
        /// </summary>
        protected StrongNameException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            // do nothing
        }
    }
}
