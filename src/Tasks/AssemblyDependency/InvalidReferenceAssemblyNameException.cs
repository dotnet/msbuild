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
    /// There reference is not a well-formed fusion name *and* its not a file 
    /// that exists on disk.
    /// </summary>
    [Serializable]
    internal sealed class InvalidReferenceAssemblyNameException : Exception
    {
        private string sourceItemSpec;

        /// <summary>
        /// Don't allow default construction.
        /// </summary>
        private InvalidReferenceAssemblyNameException()
        {
        }

        /// <summary>
        /// Construct
        /// </summary>
        internal InvalidReferenceAssemblyNameException(string sourceItemSpec)
        {
            this.sourceItemSpec = sourceItemSpec;
        }

#if FEATURE_BINARY_SERIALIZATION
        /// <summary>
        /// Construct
        /// </summary>
        private InvalidReferenceAssemblyNameException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif

        /// <summary>
        /// The item spec of the item that is the source fo the problem.
        /// </summary>
        internal string SourceItemSpec
        {
            get { return sourceItemSpec; }
        }
    }
}
