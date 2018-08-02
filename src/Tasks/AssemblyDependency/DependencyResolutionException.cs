// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

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
        private DependencyResolutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
