// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.CommandLine;
using System.CommandLine.Parsing;
using System;

namespace Microsoft.DotNet.Tools.Build
{
    public class BuildCommand : RestoringCommand
    {
        public BuildCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, noRestore, msbuildPath)
        {
        }

        public static BuildCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Cli.Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet build", args);
            return FromParseResult(parseResult, msbuildPath);
        }

        public static BuildCommand FromParseResult(ParseResult parseResult, string msbuildPath = null)
        {
            PerformanceLogEventSource.Log.CreateBuildCommandStart();
             
            var msbuildArgs = new List<string>();

            parseResult.ShowHelpOrErrorIfAppropriate();

            CommonOptions.ValidateSelfContainedOptions(parseResult.HasOption(BuildCommandParser.SelfContainedOption),
                parseResult.HasOption(BuildCommandParser.NoSelfContainedOption));

            msbuildArgs.Add($"-consoleloggerparameters:Summary");

            if (parseResult.HasOption(BuildCommandParser.NoIncrementalOption))
            {
                msbuildArgs.Add("-target:Rebuild");
            }
            var arguments = parseResult.GetValueForArgument(BuildCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>();

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(BuildCommandParser.GetCommand()));

            msbuildArgs.AddRange(arguments);

            bool noRestore = parseResult.HasOption(BuildCommandParser.NoRestoreOption);

            BuildCommand command = new BuildCommand(
                msbuildArgs,
                noRestore,
                msbuildPath);

            PerformanceLogEventSource.Log.CreateBuildCommandStop();

            return command;
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
