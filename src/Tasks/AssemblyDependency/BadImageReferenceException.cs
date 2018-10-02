// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// The reference points to a bad image.
    /// </summary>
    [Serializable]
    internal sealed class BadImageReferenceException : Exception
    {
        /// <summary>
        /// Construct
        /// </summary>
        internal BadImageReferenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Construct
        /// </summary>
        private BadImageReferenceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
