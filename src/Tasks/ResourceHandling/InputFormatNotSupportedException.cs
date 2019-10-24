// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    [Serializable]
    internal class InputFormatNotSupportedException : Exception
    {
        public InputFormatNotSupportedException()
        {
        }

        public InputFormatNotSupportedException(string message) : base(message)
        {
        }

        public InputFormatNotSupportedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InputFormatNotSupportedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
