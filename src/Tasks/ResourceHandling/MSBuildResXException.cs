// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    [Serializable]
    internal class MSBuildResXException : Exception
    {
        public MSBuildResXException()
        {
        }

        public MSBuildResXException(string message) : base(message)
        {
        }

        public MSBuildResXException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected MSBuildResXException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
