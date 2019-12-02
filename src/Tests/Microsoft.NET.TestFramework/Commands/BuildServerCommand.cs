// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class BuildServerCommand : DotnetCommand
    {
        public BuildServerCommand(ITestOutputHelper log, params string[] args) : base(log, args)
        {
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            List<string> newArgs = new List<string>()
            {
                "build-server"
            };
            newArgs.AddRange(args);

            return base.CreateCommand(newArgs);
        }
    }
}
