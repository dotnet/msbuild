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
