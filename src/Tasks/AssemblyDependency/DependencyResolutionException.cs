// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Exception indicates a problem finding dependencies of a reference.
    /// </summary>
    [Serializable]
    internal sealed class DependencyResolutionException : Exception
    {
        /// <summary>
        /// Construct
        /// </summary>
        internal DependencyResolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Construct
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        private DependencyResolutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
