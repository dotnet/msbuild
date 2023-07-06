// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Format
{
    internal static partial class FormatCommandParser
    {
        public static readonly CliArgument<string[]> Arguments = new("arguments");

        public static readonly string DocsLink = "https://aka.ms/dotnet-format";

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var formatCommand = new DocumentedCommand("format", DocsLink)
            {
                Arguments
            };
            formatCommand.SetAction((ParseResult parseResult) => FormatCommand.Run(parseResult.GetValue(Arguments)));
            return formatCommand;
        }
    }
}
