// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class BuildServerCommand : DotnetCommand
    {
        public override CommandResult Execute(string args = "")
        {
            return base.Execute($"build-server {args}");
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            return base.ExecuteWithCapturedOutput($"build-server {args}");
        }
    }
}
