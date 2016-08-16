// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.TestFramework.Commands
{
    public class MSBuildTest
    {
        public static readonly MSBuildTest Stage0MSBuild = new MSBuildTest(GetStage0Path());

        private string BinPath { get; }

        public MSBuildTest(string binPath)
        {
            BinPath = binPath;
        }

        public Command CreateCommandForTarget(string target, params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, $"/t:{target}");

            return CreateCommand(newArgs.ToArray());
        }

        private Command CreateCommand(params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, $"build3");

            return Command.Create(Path.Combine(BinPath, $"dotnet{Constants.ExeSuffix}"), newArgs);
        }

        private static string GetStage0Path()
        {
            return Path.Combine(RepoInfo.RepoRoot, ".dotnet_cli");
        }
    }
}