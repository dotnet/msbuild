// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Format
{
    internal static partial class FormatCommandParser
    {
        public static readonly Argument<string[]> Arguments = new();

        public static readonly string DocsLink = "https://aka.ms/dotnet-format";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var formatCommand = new DocumentedCommand("format", DocsLink)
            {
                Arguments
            };
            formatCommand.SetHandler((ParseResult parseResult) => FormatCommand.Run(parseResult.GetValue(Arguments)));
            return formatCommand;
        }
    }
}
