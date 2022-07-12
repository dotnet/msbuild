// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Store
{
    public class StoreCommand : MSBuildForwardingApp
    {
        private StoreCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static StoreCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Parser.Instance;
            var result = parser.ParseFrom("dotnet store", args);
            return FromParseResult(result, msbuildPath);
        }

        public static StoreCommand FromParseResult(ParseResult result, string msbuildPath = null)
        {
            var msbuildArgs = new List<string>();

            result.ShowHelpOrErrorIfAppropriate();

            if (!result.HasOption(StoreCommandParser.ManifestOption))
            {
                throw new GracefulException(LocalizableStrings.SpecifyManifests);
            }

            msbuildArgs.Add("-target:ComposeStore");

            msbuildArgs.AddRange(result.OptionValuesToBeForwarded(StoreCommandParser.GetCommand()));

            msbuildArgs.AddRange(result.GetValueForArgument(StoreCommandParser.Argument) ?? Array.Empty<string>());

            return new StoreCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
