﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Internal exception thrown when there's an unrecoverable failure resolving a COM reference and we should 
    /// move on to the next one, if it makes sense.
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both forward and backward compatibility
    [Serializable]
    internal class ComReferenceResolutionException : Exception
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        internal ComReferenceResolutionException()
        {
            // do nothing
        }

        /// <summary>
        /// Constructor that allows to preserve the original exception information
        /// </summary>
        internal ComReferenceResolutionException(Exception innerException) : base("", innerException)
        {
            // do nothing
        }

        /// <summary>
        /// Deserializing constructor. It should not be necessary if everything goes well, but if it doesn't
        /// then we don't want to crash when trying to deserialize the uncaught exception into another AppDomain.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected ComReferenceResolutionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            // do nothing
        }
    }
}
