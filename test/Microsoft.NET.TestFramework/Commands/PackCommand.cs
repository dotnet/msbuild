// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using System.IO;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class PackCommand : TestCommand
    {
        public PackCommand(MSBuildTest msbuild, string projectPath, string relativePathToProject = null)
            : base(msbuild, projectPath, relativePathToProject)
        {
        }

        public override CommandResult Execute(params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, FullPathProjectFile);

            var command = MSBuild.CreateCommandForTarget("pack", newArgs.ToArray());

            return command.Execute();
        }

        public string GetIntermediateNuspecPath(string packageId = null, string packageVersion = "1.0.0")
        {
            if (packageId == null)
            {
                packageId = Path.GetFileNameWithoutExtension(ProjectFile);
            }

            return Path.Combine(GetBaseIntermediateDirectory().FullName, $"{packageId}.{packageVersion}.nuspec");
        }
    }
}
