// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolPackage
{
    internal class NuGetPackageDownloaderException : Exception
    {
        public NuGetPackageDownloaderException()
        {
        }

        public NuGetPackageDownloaderException(string message) : base(message)
        {
        }

        public NuGetPackageDownloaderException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
