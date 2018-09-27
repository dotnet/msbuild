// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestCannotFindException : Exception
    {
        public ToolManifestCannotFindException()
        {
        }

        public ToolManifestCannotFindException(string message) : base(message)
        {
        }

        public ToolManifestCannotFindException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
