// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Cli;
using System;
using System.CommandLine;
using System.CommandLine.Parsing;

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

            msbuildArgs.AddRange(result.GetValueForArgument(CleanCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>());

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
