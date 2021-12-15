// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using static Microsoft.DotNet.Tools.Format.FormatCommandCommon;

namespace Microsoft.DotNet.Tools.Format
{
    internal static partial class FormatCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-format";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var formatCommand = new DocumentedCommand("format", DocsLink, LocalizableStrings.Formats_code_to_match_editorconfig_settings)
            {
                FormatWhitespaceCommandParser.GetCommand(),
                FormatStyleCommandParser.GetCommand(),
                FormatAnalyzersCommandParser.GetCommand(),
                DiagnosticsOption,
                SeverityOption,
            };
            formatCommand.AddCommonOptions();
            formatCommand.SetHandler((ParseResult parseResult) => FormatCommand.Run(parseResult.GetArguments()));
            return formatCommand;
        }
    }
}
