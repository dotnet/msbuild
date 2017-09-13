// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    public class FailedToAddPackageToPackageRepositoryException : Exception
    {
        public FailedToAddPackageToPackageRepositoryException(string message) : base(message)
        {
        }

        public FailedToAddPackageToPackageRepositoryException()
        {
        }

        public FailedToAddPackageToPackageRepositoryException(string message, Exception innerException) : base(message,
            innerException)
        {
        }
    }
}
