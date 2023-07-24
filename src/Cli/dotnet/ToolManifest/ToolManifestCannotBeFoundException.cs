// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
