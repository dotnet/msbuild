// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestException : Exception
    {
        public ToolManifestException()
        {
        }

        public ToolManifestException(string message) : base(message)
        {
        }

        public ToolManifestException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
