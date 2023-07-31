// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
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
