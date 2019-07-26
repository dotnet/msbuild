// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

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
