// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// There reference is not a well-formed fusion name *and* its not a file
    /// that exists on disk.
    /// </summary>
    [Serializable]
    internal sealed class InvalidReferenceAssemblyNameException : Exception
    {
        /// <summary>
        /// Construct
        /// </summary>
        internal InvalidReferenceAssemblyNameException(string sourceItemSpec)
        {
            SourceItemSpec = sourceItemSpec;
        }

        /// <summary>
        /// Construct
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        private InvalidReferenceAssemblyNameException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        /// <summary>
        /// The item spec of the item that is the source fo the problem.
        /// </summary>
        internal string SourceItemSpec { get; }
    }
}
