// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Exception indicates a problem finding dependencies of a reference.
    /// </summary>
    [Serializable]
    internal sealed class DependencyResolutionException : Exception
    {
        /// <summary>
        /// Don't allow default construction.
        /// </summary>
        private DependencyResolutionException()
        {
        }

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
