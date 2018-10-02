// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

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
        private InvalidReferenceAssemblyNameException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        /// <summary>
        /// The item spec of the item that is the source fo the problem.
        /// </summary>
        internal string SourceItemSpec { get; }
    }
}
