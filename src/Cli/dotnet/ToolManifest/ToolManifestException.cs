// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestException : GracefulException
    {
        public ToolManifestException(string message) : base(new[] { message }, null, false)
        {

        }
    }
}
