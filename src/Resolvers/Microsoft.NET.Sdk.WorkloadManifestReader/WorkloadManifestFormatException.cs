// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    class WorkloadManifestFormatException : WorkloadManifestException
    {
        public WorkloadManifestFormatException() { }
        public WorkloadManifestFormatException(string messageFormat, params object?[] args) : base(messageFormat, args) { }
        public WorkloadManifestFormatException(string message) : base(message) { }
        public WorkloadManifestFormatException(string message, Exception inner) : base(message, inner) { }
    }

    class WorkloadManifestCompositionException : WorkloadManifestException
    {
        public WorkloadManifestCompositionException() { }
        public WorkloadManifestCompositionException(string messageFormat, params object?[] args) : base(messageFormat, args) { }
        public WorkloadManifestCompositionException(string message) : base(message) { }
        public WorkloadManifestCompositionException(string message, Exception inner) : base(message, inner) { }
    }

    public abstract class WorkloadManifestException : Exception
    {
        protected WorkloadManifestException() { }
        protected WorkloadManifestException(string messageFormat, params object?[] args) : base(string.Format(messageFormat, args)) { }
        protected WorkloadManifestException(string message) : base(message) { }
        protected WorkloadManifestException(string message, Exception inner) : base(message, inner) { }
    }
}
