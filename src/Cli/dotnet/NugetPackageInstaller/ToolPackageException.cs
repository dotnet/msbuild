// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolPackage
{
    internal class NuGetPackageInstallerException : Exception
    {
        public NuGetPackageInstallerException()
        {
        }

        public NuGetPackageInstallerException(string message) : base(message)
        {
        }

        public NuGetPackageInstallerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
