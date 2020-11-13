// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.Linq;
using static Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Cli
{
    public static class ParseResultExtensions
    {
        public static void ShowHelp(this ParseResult parseResult)
        {
            DotnetHelpBuilder.Instance.Value.Write(parseResult.CommandResult.Command);
        }

        public static void ShowHelpOrErrorIfAppropriate(this ParseResult parseResult)
        {
            if (parseResult.Errors.Any())
            {
                throw new CommandParsingException(
                    message: string.Join(Environment.NewLine,
                                         parseResult.Errors.Select(e => e.Message)));
            }
            else if (parseResult.HasOption("--help"))
            {
                parseResult.ShowHelp();
                throw new HelpException(string.Empty);
            }
        }

        public static string RootSubCommandResult(this ParseResult parseResult)
        {
            return parseResult.RootCommandResult.Children?
                .FirstOrDefault(c => c.Token() != null && c.Token().Type.Equals(TokenType.Command))?
                .Symbol.Name ?? string.Empty;
        }
    }
}
