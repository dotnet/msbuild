// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
