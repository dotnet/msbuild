// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.Serialization;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// There was a problem resolving this reference into a full file name.
    /// </summary>
    [Serializable]
    internal sealed class ReferenceResolutionException : Exception
    {
        /// <summary>
        /// Don't allow default construction.
        /// </summary>
        private ReferenceResolutionException()
        {
        }

        /// <summary>
        /// Construct
        /// </summary>
        internal ReferenceResolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if FEATURE_BINARY_SERIALIZATION
        /// <summary>
        /// Implement required constructors for serialization
        /// </summary>
        private ReferenceResolutionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}
