// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// There was a problem resolving this reference into a full file name.
    /// </summary>
    [Serializable]
    internal sealed class ReferenceResolutionException : Exception
    {
        /// <summary>
        /// Construct
        /// </summary>
        internal ReferenceResolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Implement required constructors for serialization
        /// </summary>
        private ReferenceResolutionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
