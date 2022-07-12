// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace Microsoft.DotNet.Cli
{
    public static class ParserExtensions
    {
        public static ParseResult ParseFrom(
            this System.CommandLine.Parsing.Parser parser,
            string context,
            string[] args = null) =>
            parser.Parse(context.Split(' ').Concat(args).ToArray());
    }
}
