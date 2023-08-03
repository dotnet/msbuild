// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
