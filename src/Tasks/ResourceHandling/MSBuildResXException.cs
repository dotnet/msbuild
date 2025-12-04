// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Tasks.ResourceHandling
{
    [Serializable]
    internal class MSBuildResXException : Exception
    {
        public MSBuildResXException()
        {
        }

        public MSBuildResXException(string message) : base(message)
        {
        }

        public MSBuildResXException(string message, Exception innerException) : base(message, innerException)
        {
        }

#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        protected MSBuildResXException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
