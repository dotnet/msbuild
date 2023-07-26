// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
