// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public sealed class RestoreCommand : TestCommand
    {
        public RestoreCommand()
            : base("dotnet")
        {

        }

        public override CommandResult Execute(string args="")
        {
            args = $"restore {args}";
            return base.Execute(args);
        }
    }
}
