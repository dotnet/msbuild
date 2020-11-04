// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace Microsoft.DotNet.Cli
{
    public static class ParseResultExtensions
    {
        public static void ShowHelp(this ParseResult parseResult)
        {
            Parser.Instance.Invoke(parseResult.Tokens.Select(t => t.Value).Append("-h").ToArray());
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
    }
}
