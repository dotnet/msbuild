// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Cli;
using System.CommandLine;

namespace Microsoft.DotNet.Tools.Clean
{
    public class CleanCommand : MSBuildForwardingApp
    {
        public CleanCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static CleanCommand FromArgs(string[] args, string msbuildPath = null)
        {

            var parser = Cli.Parser.Instance;
            var result = parser.ParseFrom("dotnet clean", args);
            return FromParseResult(result, msbuildPath);
        }

        public static CleanCommand FromParseResult(ParseResult result, string msbuildPath = null)
        {
            var msbuildArgs = new List<string>
            {
                "-verbosity:normal"
            };

            result.ShowHelpOrErrorIfAppropriate();

            msbuildArgs.AddRange(result.GetValue(CleanCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>());

            msbuildArgs.Add("-target:Clean");

            msbuildArgs.AddRange(result.OptionValuesToBeForwarded(CleanCommandParser.GetCommand()));

            return new CleanCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
