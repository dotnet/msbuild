// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging
{
    public class StreamChunkOverReadException : Exception
    {
        public StreamChunkOverReadException()
        {
        }

        public StreamChunkOverReadException(string message) : base(message)
        {
        }

        public StreamChunkOverReadException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
