using System;

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{
    [Serializable]
    class WorkloadManifestFormatException: Exception
    {
        public WorkloadManifestFormatException() { }
        public WorkloadManifestFormatException(string message) : base(message) { }
        public WorkloadManifestFormatException(string message, Exception inner) : base(message, inner) { }
        protected WorkloadManifestFormatException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
