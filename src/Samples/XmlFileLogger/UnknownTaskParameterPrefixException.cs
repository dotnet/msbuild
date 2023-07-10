// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Logging.StructuredLogger
{
    internal sealed class UnknownTaskParameterPrefixException : Exception
    {
        public UnknownTaskParameterPrefixException(string prefix)
            : base(string.Format("Unknown task parameter type: {0}", prefix))
        {
        }
    }
}
