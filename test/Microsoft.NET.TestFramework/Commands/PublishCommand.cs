// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class PublishCommand : TestCommand
    {
        private const string PublishSubfolderName = "publish";

        public PublishCommand(MSBuildTest msbuild, string projectPath)
            : base(msbuild, projectPath)
        {
        }

        public override CommandResult Execute(params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, FullPathProjectFile);

            var command = MSBuild.CreateCommandForTarget("publish", newArgs.ToArray());

            return command.Execute();
        }

        public override DirectoryInfo GetOutputDirectory(string targetFramework = "netcoreapp1.1", string configuration = "Debug", string runtimeIdentifier = "")
        {
            DirectoryInfo baseDirectory = base.GetOutputDirectory(targetFramework, configuration, runtimeIdentifier); 
            return new DirectoryInfo(Path.Combine(baseDirectory.FullName, PublishSubfolderName));
        }

        public string GetPublishedAppPath(string appName)
        {
            return Path.Combine(GetOutputDirectory().FullName, $"{appName}.dll");
        }
    }
}
