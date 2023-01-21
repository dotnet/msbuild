// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Exceptions
{
    /// <summary>
    /// An exception representing the case where a BuildRequest has caused a circular project dependency.  This is used to
    /// terminate the request builder which initiated the failure path.
    /// </summary>
    /// <remarks>
    /// If you add fields to this class, add a custom serialization constructor and override GetObjectData().
    /// </remarks>
    [Serializable]
    public class CircularDependencyException : Exception
    {
        /// <summary>
        /// Constructs a standard BuildAbortedException.
        /// </summary>
        internal CircularDependencyException()
        {
        }

        internal CircularDependencyException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        protected CircularDependencyException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
