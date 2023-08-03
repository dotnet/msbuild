// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using System.CommandLine;
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

            msbuildArgs.AddRange(result.GetValue(StoreCommandParser.Argument) ?? Array.Empty<string>());

            return new StoreCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
