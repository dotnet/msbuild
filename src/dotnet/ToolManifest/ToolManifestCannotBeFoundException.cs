// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestCannotBeFoundException : GracefulException
    {
        public ToolManifestCannotBeFoundException(string message) : base(new[] { message }, null, false)
        {
        }

        public ToolManifestCannotBeFoundException(string message, string optionalMessage)
            : base(new[] { message }, new[] { optionalMessage }, false)
        {
        }
    }
}
