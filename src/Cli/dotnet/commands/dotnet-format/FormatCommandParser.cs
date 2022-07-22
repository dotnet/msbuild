// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            formatCommand.SetHandler((ParseResult parseResult) => FormatCommand.Run(parseResult.GetValueForArgument(Arguments)));
            return formatCommand;
        }
    }
}
