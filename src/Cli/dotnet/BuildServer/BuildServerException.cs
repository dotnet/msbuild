// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.BuildServer
{
    internal class BuildServerException : Exception
    {
        public BuildServerException()
        {
        }

        public BuildServerException(string message) : base(message)
        {
        }

        public BuildServerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
