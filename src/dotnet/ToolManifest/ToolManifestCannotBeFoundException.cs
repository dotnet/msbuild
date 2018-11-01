// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestCannotBeFoundException : Exception
    {
        public ToolManifestCannotBeFoundException()
        {
        }

        public ToolManifestCannotBeFoundException(string message) : base(message)
        {
        }

        public ToolManifestCannotBeFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
