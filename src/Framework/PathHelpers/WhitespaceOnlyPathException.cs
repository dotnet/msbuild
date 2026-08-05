// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.Framework
{
    [Serializable]
    internal sealed class WhitespaceOnlyPathException : ArgumentException
    {
        internal WhitespaceOnlyPathException(string paramName)
            : base(SR.PathCannotBeWhitespace, paramName)
        {
        }

#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        private WhitespaceOnlyPathException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
