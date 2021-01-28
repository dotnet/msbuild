// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.CommandLine.Parsing;
using System;
using System.Linq;

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
            PerformanceLogEventSource.Log.CreateBuildCommandStart();

            var msbuildArgs = new List<string>();

            var parser = Parser.Instance;

            var parseResult = parser.ParseFrom("dotnet build", args);

            parseResult.ShowHelpOrErrorIfAppropriate();

            msbuildArgs.Add($"-consoleloggerparameters:Summary");

            if (parseResult.HasOption(BuildCommandParser.NoIncrementalOption))
            {
                msbuildArgs.Add("-target:Rebuild");
            }
            var arguments = parseResult.ValueForArgument<IEnumerable<string>>(BuildCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>();

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(BuildCommandParser.GetCommand()));

            msbuildArgs.AddRange(arguments);

            bool noRestore = parseResult.HasOption(BuildCommandParser.NoRestoreOption);

            BuildCommand command = new BuildCommand(
                msbuildArgs,
                parseResult.OptionValuesToBeForwarded(BuildCommandParser.GetCommand()),
                arguments,
                noRestore,
                msbuildPath);

            PerformanceLogEventSource.Log.CreateBuildCommandStop();

            return command;
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }
    }
}
