// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Restore
{
    public class RestoreCommand : MSBuildForwardingApp
    {
        public RestoreCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static RestoreCommand FromArgs(string[] args, string msbuildPath = null, bool noLogo = true)
        {
            var parser = Parser.Instance;
            var result = parser.ParseFrom("dotnet restore", args);
            return FromParseResult(result, msbuildPath, noLogo);
        }

        public static RestoreCommand FromParseResult(ParseResult result, string msbuildPath = null, bool noLogo = true)
        {
            result.HandleDebugSwitch();

            result.ShowHelpOrErrorIfAppropriate();

            var msbuildArgs = new List<string>();

            if (noLogo)
            {
                msbuildArgs.Add("-nologo");
            }

            msbuildArgs.Add("-target:Restore");

            msbuildArgs.AddRange(result.OptionValuesToBeForwarded(RestoreCommandParser.GetCommand()));

            msbuildArgs.AddRange(result.GetValueForArgument(RestoreCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>());

            return new RestoreCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
