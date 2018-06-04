// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolPackageException : Exception
    {
        public ToolPackageException()
        {
        }

        public ToolPackageException(string message) : base(message)
        {
        }

        public ToolPackageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
