// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Fsi;

namespace Microsoft.DotNet.Cli
{
    internal static class FsiCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-fsi";

        public static readonly CliArgument<string[]> Arguments = new("arguments");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("fsi", DocsLink) { Arguments };

            command.SetAction((ParseResult parseResult) => FsiCommand.Run(parseResult.GetValue(Arguments)));

            return command;
        }
    }
}
