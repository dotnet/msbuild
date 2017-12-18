// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    public class RetryFailedException : Exception
    {
        public RetryFailedException(string message) : base(message)
        {
        }

        public RetryFailedException()
        {
        }

        public RetryFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
