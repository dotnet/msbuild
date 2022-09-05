// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Fsi;

namespace Microsoft.DotNet.Cli
{
    internal static class FsiCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-fsi";

        public static readonly Argument<string[]> Arguments = new Argument<string[]>();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("fsi", DocsLink) { Arguments };

            command.SetHandler((ParseResult parseResult) => FsiCommand.Run(parseResult.GetValueForArgument(Arguments)));

            return command;
        }
    }
}
