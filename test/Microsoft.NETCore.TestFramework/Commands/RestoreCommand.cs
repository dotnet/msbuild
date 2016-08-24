// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System.Linq;

namespace Microsoft.NETCore.TestFramework.Commands
{
    public sealed class RestoreCommand : TestCommand
    {
        public RestoreCommand(MSBuildTest msbuild, string projectPath) : base(msbuild, projectPath)
        {
        }

        public override CommandResult Execute(params string[] args)
        {
            var newArgs = args.ToList();

            // TODO: use MSBuild when https://github.com/dotnet/sdk/issues/75 is fixed
            //newArgs.Insert(0, FullPathProjectFile);
            //var command = MSBuild.CreateCommandForTarget("restore", newArgs.ToArray());

            newArgs.Insert(0, ProjectRootPath);
            newArgs.Insert(0, "restore");
            var command = Command.Create(RepoInfo.DotNetHostPath, newArgs);

            return command.Execute();
        }
    }
}
