// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Restore;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolListCommandParser
    {
        public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption(LocalizableStrings.GlobalOptionDescription);

        public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption(LocalizableStrings.LocalOptionDescription);

        public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption(LocalizableStrings.ToolPathOptionDescription, LocalizableStrings.ToolPathOptionName);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("list", LocalizableStrings.CommandDescription);

            command.AddOption(GlobalOption);
            command.AddOption(LocalOption);
            command.AddOption(ToolPathOption);

            CommandHandler.Create<ParseResult>((parseResult) => new ToolRestoreCommand(parseResult).Execute());

            return command;
        }
    }
}
