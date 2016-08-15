// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System.Linq;

namespace Microsoft.DotNet.TestFramework.Commands
{
    public sealed class RestoreCommand : TestCommand
    {
        public RestoreCommand(MSBuildTest msbuild, string projectPath) : base(msbuild, projectPath)
        {
        }

        public override CommandResult Execute(params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, FullPathProjectFile);

            var command = MSBuild.CreateCommandForTarget("restore", newArgs.ToArray());

            return command.Execute();
        }
    }
}
