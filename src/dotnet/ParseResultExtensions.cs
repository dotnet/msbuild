// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class ParserExtensions
    {
        public static ParseResult ParseFrom(
            this CommandLine.Parser parser,
            string context,
            string[] args) =>
            parser.Parse(context.Split(' ').Concat(args).ToArray());
    }

    public static class ParseResultExtensions
    {
        public static void ShowHelp(this ParseResult parseResult) =>
            Console.WriteLine(parseResult.Command().HelpView());
    }
}