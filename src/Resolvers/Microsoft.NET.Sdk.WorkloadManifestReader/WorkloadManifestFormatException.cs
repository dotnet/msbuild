// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    [Serializable]
    class WorkloadManifestFormatException: Exception
    {
        public WorkloadManifestFormatException() { }
        public WorkloadManifestFormatException(string messageFormat, params object[] args) : base(string.Format (messageFormat, args)) { }
        public WorkloadManifestFormatException(string message) : base(message) { }
        public WorkloadManifestFormatException(string message, Exception inner) : base(message, inner) { }
        protected WorkloadManifestFormatException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
