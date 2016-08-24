// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NETCore.TestFramework.Commands
{
    public sealed class BuildCommand : TestCommand
    {
        public BuildCommand(MSBuildTest msbuild, string projectPath)
            : base(msbuild, projectPath)
        {
        }

        public override CommandResult Execute(params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, FullPathProjectFile);

            var command = MSBuild.CreateCommandForTarget("build", newArgs.ToArray());

            return command.Execute();
        }

        public DirectoryInfo GetOutputDirectory()
        {
            string output = Path.Combine(ProjectRootPath, "bin", "Debug");
            return new DirectoryInfo(output);
        }
    }
}
