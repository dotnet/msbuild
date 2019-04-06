// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class ParseResultExtensions
    {
        public static void ShowHelp(this ParseResult parseResult) =>
            Console.WriteLine(parseResult.Command().HelpView().TrimEnd());

        public static void ShowHelpOrErrorIfAppropriate(this ParseResult parseResult)
        {
            parseResult.ShowHelpIfRequested();

            if (parseResult.Errors.Any())
            {
                throw new CommandParsingException(
                    message: string.Join(Environment.NewLine,
                                         parseResult.Errors.Select(e => e.Message)),
                    helpText: parseResult?.Command()?.HelpView().TrimEnd());
            }
        }

        public static void ShowHelpIfRequested(this ParseResult parseResult)
        {
            if (parseResult.AppliedCommand().IsHelpRequested())
            {
                // NOTE: this is a temporary stage in refactoring toward the ClicCommandLineParser being used at the CLI entry point. 
                throw new HelpException(parseResult.Command().HelpView().TrimEnd());
            }
        }
    }
}