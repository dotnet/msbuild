// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;

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

            CommonOptions.ValidateSelfContainedOptions(
                parseResult.GetResult(BuildCommandParser.SelfContainedOption) is not null,
                parseResult.GetResult(BuildCommandParser.NoSelfContainedOption) is not null);

            msbuildArgs.Add($"-consoleloggerparameters:Summary");

            if (parseResult.GetResult(BuildCommandParser.NoIncrementalOption) is not null)
            {
                msbuildArgs.Add("-target:Rebuild");
            }
            var arguments = parseResult.GetValue(BuildCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>();

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(BuildCommandParser.GetCommand()));

            msbuildArgs.AddRange(arguments);

            bool noRestore = parseResult.GetResult(BuildCommandParser.NoRestoreOption) is not null;

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
