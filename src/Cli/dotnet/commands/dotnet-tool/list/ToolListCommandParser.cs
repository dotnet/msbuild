// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.List;
using Microsoft.DotNet.Tools.Tool.Restore;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolListCommandParser
    {
        public static readonly Argument<string> PackageIdArgument = new Argument<string>(LocalizableStrings.PackageIdArgumentName)
        {
            Description = LocalizableStrings.PackageIdArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne,
        };

        public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("list", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(GlobalOption.WithHelpDescription(command, LocalizableStrings.GlobalOptionDescription));
            command.AddOption(LocalOption.WithHelpDescription(command, LocalizableStrings.LocalOptionDescription));
            command.AddOption(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolPathOptionDescription));

            command.SetHandler((parseResult) => new ToolListCommand(parseResult).Execute());

            return command;
        }
    }
}
