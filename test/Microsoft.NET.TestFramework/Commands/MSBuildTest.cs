// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Commands
{
    public class MSBuildTest
    {
        public static readonly MSBuildTest Stage0MSBuild = new MSBuildTest(RepoInfo.DotNetHostPath);

        private string DotNetHostPath { get; }

        public MSBuildTest(string dotNetHostPath)
        {
            DotNetHostPath = dotNetHostPath;
        }

        public ICommand CreateCommandForTarget(string target, params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, $"/t:{target}");

            return CreateCommand(newArgs.ToArray());
        }

        private ICommand CreateCommand(params string[] args)
        {
            ICommand command;

            //  Run tests on full framework MSBuild if environment variable is set pointing to it
            string msbuildPath = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MSBUILD_PATH");
            if (!string.IsNullOrEmpty(msbuildPath))
            {
                command = Command.Create(msbuildPath, args);
            }
            else
            {
                var newArgs = args.ToList();
                newArgs.Insert(0, $"msbuild");

                command = Command.Create(DotNetHostPath, newArgs);
            }

            //  Set NUGET_PACKAGES environment variable to match value from build.ps1
            command = command.EnvironmentVariable("NUGET_PACKAGES", Path.Combine(RepoInfo.RepoRoot, "packages"));

            command = command.EnvironmentVariable("MSBuildSDKsPath", RepoInfo.SdksPath);

            return command;
        }
    }
}