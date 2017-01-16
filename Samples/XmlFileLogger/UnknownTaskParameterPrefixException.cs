// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    internal class UnknownTaskParameterPrefixException : Exception
    {
        public UnknownTaskParameterPrefixException(string prefix)
            : base(string.Format("Unknown task parameter type: {0}", prefix))
        {
        }
    }
}
