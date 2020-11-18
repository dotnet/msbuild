// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Publish
{
    public class PublishCommand : RestoringCommand
    {
        private PublishCommand(
            IEnumerable<string> msbuildArgs,
            IEnumerable<string> userDefinedArguments,
            IEnumerable<string> trailingArguments,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, userDefinedArguments, trailingArguments, noRestore, msbuildPath)
        {
        }

        public static PublishCommand FromArgs(string[] args, string msbuildPath = null)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var msbuildArgs = new List<string>();

            var parser = Parser.Instance;

            var parseResult = parser.ParseFrom("dotnet publish", args);

            parseResult.ShowHelpOrErrorIfAppropriate();

            msbuildArgs.Add("-target:Publish");

            if (parseResult.HasOption(PublishCommandParser.SelfContainedOption) &&
                parseResult.HasOption(PublishCommandParser.NoSelfContainedOption))
            {
                throw new GracefulException(LocalizableStrings.SelfContainAndNoSelfContainedConflict);
            }

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(PublishCommandParser.GetCommand()));

            msbuildArgs.AddRange(parseResult.ValueForArgument<IEnumerable<string>>(PublishCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>());

            bool noRestore = parseResult.HasOption(PublishCommandParser.NoRestoreOption)
                          || parseResult.HasOption(PublishCommandParser.NoBuildOption);

            return new PublishCommand(
                msbuildArgs,
                parseResult.OptionValuesToBeForwarded(PublishCommandParser.GetCommand()),
                parseResult.ValueForArgument<IEnumerable<string>>(PublishCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>(),
                noRestore,
                msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }
    }
}
