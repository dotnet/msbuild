// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Tasks.ResourceHandling
{
    [Serializable]
    internal sealed class PreserializedResourceWriterRequiredException : Exception
    {
        public PreserializedResourceWriterRequiredException() { }

        private PreserializedResourceWriterRequiredException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
