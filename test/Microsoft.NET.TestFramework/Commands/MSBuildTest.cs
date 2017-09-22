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

        public SdkCommandSpec CreateCommandForTarget(string target, params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, $"/t:{target}");

            return CreateCommand(newArgs.ToArray());
        }

        private SdkCommandSpec CreateCommand(params string[] args)
        {
            SdkCommandSpec ret = new SdkCommandSpec();

            //  Run tests on full framework MSBuild if environment variable is set pointing to it
            string msbuildPath = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MSBUILD_PATH");
            if (!string.IsNullOrEmpty(msbuildPath))
            {
                ret.FileName = msbuildPath;
                ret.Arguments = args.ToList();
            }
            else
            {
                var newArgs = args.ToList();
                newArgs.Insert(0, $"msbuild");

                ret.FileName = DotNetHostPath;
                ret.Arguments = newArgs;
            }

            RepoInfo.AddTestEnvironmentVariables(ret);

            return ret;
        }
    }
}
