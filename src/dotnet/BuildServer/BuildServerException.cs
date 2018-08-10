// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

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
