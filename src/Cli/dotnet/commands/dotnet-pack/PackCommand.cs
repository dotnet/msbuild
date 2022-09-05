// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;
using System;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Pack
{
    public class PackCommand : RestoringCommand
    {
        public PackCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, noRestore, msbuildPath)
        {
        }

        public static PackCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet pack", args);
            return FromParseResult(parseResult, msbuildPath);
        }

        public static PackCommand FromParseResult(ParseResult parseResult, string msbuildPath = null)
        {
            parseResult.ShowHelpOrErrorIfAppropriate();

            var msbuildArgs = new List<string>()
            {
                "-target:pack",
                "-property:_IsPacking=true"
            };

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(PackCommandParser.GetCommand()));

            msbuildArgs.AddRange(parseResult.GetValueForArgument(PackCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>());

            bool noRestore = parseResult.HasOption(PackCommandParser.NoRestoreOption) || parseResult.HasOption(PackCommandParser.NoBuildOption);

            return new PackCommand(
                msbuildArgs,
                noRestore,
                msbuildPath);
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
