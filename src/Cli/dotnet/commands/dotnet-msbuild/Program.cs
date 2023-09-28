// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public class MSBuildCommand : MSBuildForwardingApp
    {
        public MSBuildCommand
            (IEnumerable<string> msbuildArgs,
            string msbuildPath = null)
             : base(msbuildArgs, msbuildPath)
        {
        }

        public static MSBuildCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Parser.Instance;
            var result = parser.ParseFrom("dotnet msbuild", args);
            return FromParseResult(result, msbuildPath);
        }

        public static MSBuildCommand FromParseResult(ParseResult parseResult, string msbuildPath = null)
        {
            var msbuildArgs = new List<string>();

            msbuildArgs.AddRange(parseResult.GetValue(MSBuildCommandParser.Arguments));

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(MSBuildCommandParser.GetCommand()));

            MSBuildCommand command = new(
                msbuildArgs,
                msbuildPath);
            return command;
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
