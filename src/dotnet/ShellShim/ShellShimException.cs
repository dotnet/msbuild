// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ShellShim
{
    internal class ShellShimException : Exception
    {
        public ShellShimException()
        {
        }

        public ShellShimException(string message) : base(message)
        {
        }

        public ShellShimException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
