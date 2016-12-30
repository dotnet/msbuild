// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;

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

        public DirectoryInfo GetOutputDirectory(string targetFramework = "netcoreapp1.0", string configuration = "Debug", string runtimeIdentifier = "", bool selfContained = false)
        {
            string output = Path.Combine(ProjectRootPath, "bin", BuildRelativeOutputPath(targetFramework, configuration, runtimeIdentifier, selfContained));
            return new DirectoryInfo(output);
        }

        public string GetPublishedAppPath(string appName)
        {
            return Path.Combine(GetOutputDirectory().FullName, $"{appName}.dll");
        }

        private string BuildRelativeOutputPath(string targetFramework, string configuration, string runtimeIdentifier, bool selfContained)
        {
            if (runtimeIdentifier.Length == 0 && selfContained)
            {
                runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            }
			
            return Path.Combine(configuration, targetFramework, runtimeIdentifier, PublishSubfolderName);
        }
    }
}
