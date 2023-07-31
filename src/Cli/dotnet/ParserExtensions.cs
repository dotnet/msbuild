// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class ParserExtensions
    {
        public static ParseResult ParseFrom(
            this CliConfiguration parser,
            string context,
            string[] args = null) =>
            parser.Parse(context.Split(' ').Concat(args).ToArray());
    }
}
