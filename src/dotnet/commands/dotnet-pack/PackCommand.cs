// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Cli;
using System.Diagnostics;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Pack
{
    public class PackCommand : RestoringCommand
    {
        public PackCommand(
            IEnumerable<string> msbuildArgs,
            IEnumerable<string> userDefinedArguments,
            IEnumerable<string> trailingArguments,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, userDefinedArguments, trailingArguments, noRestore, msbuildPath)
        {
        }

        public static PackCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet pack", args);

            result.ShowHelpOrErrorIfAppropriate();

            var parsedPack = result["dotnet"]["pack"];

            var msbuildArgs = new List<string>()
            {
                "-target:pack"
            };

            msbuildArgs.AddRange(parsedPack.OptionValuesToBeForwarded());

            msbuildArgs.AddRange(parsedPack.Arguments);

            bool noRestore = parsedPack.HasOption("--no-restore") || parsedPack.HasOption("--no-build");

            return new PackCommand(
                msbuildArgs,
                parsedPack.OptionValuesToBeForwarded(),
                parsedPack.Arguments,
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
