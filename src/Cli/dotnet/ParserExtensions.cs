// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
