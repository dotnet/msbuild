// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;


namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// The reference points to a bad image.
    /// </summary>
    [Serializable]
    // CodeQL [SM02227] False positive: static call-graph over-approximation via virtual ToString/Flush in VMR build; deserialization only reads primitive fields.
    internal sealed class BadImageReferenceException : Exception
    {
        /// <summary>
        /// Construct
        /// </summary>
        internal BadImageReferenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Construct
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        private BadImageReferenceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Gets a message that describes the exception.
        /// </summary>
        public override string Message => (InnerException == null) ? base.Message : $"{base.Message} {InnerException.Message}";
    }
}
