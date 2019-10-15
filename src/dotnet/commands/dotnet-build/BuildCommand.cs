// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Restore;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Build
{
    public class BuildCommand : RestoringCommand
    {
        public BuildCommand(
            IEnumerable<string> msbuildArgs,
            IEnumerable<string> userDefinedArguments,
            IEnumerable<string> trailingArguments,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, userDefinedArguments, trailingArguments, noRestore, msbuildPath)
        {
        }

        public static BuildCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var msbuildArgs = new List<string>();

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet build", args);

            result.ShowHelpOrErrorIfAppropriate();

            var appliedBuildOptions = result["dotnet"]["build"];

            msbuildArgs.Add($"-consoleloggerparameters:Summary");

            if (appliedBuildOptions.HasOption("--no-incremental"))
            {
                msbuildArgs.Add("-target:Rebuild");
            }

            msbuildArgs.AddRange(appliedBuildOptions.OptionValuesToBeForwarded());

            msbuildArgs.AddRange(appliedBuildOptions.Arguments);

            bool noRestore = appliedBuildOptions.HasOption("--no-restore");

            return new BuildCommand(
                msbuildArgs,
                appliedBuildOptions.OptionValuesToBeForwarded(),
                appliedBuildOptions.Arguments,
                noRestore,
                msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }
    }
}
