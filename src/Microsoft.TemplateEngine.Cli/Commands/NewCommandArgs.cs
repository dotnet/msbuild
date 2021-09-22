// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class NewCommandArgs : GlobalArgs
    {
        public NewCommandArgs(ParseResult parseResult) : base(parseResult)
        {
            Arguments = parseResult.ValueForArgument(RemainingArguments);
            ShortName = parseResult.ValueForArgument(ShortNameArgument);
            HelpRequested = parseResult.ValueForOption(HelpOption);
        }

        internal string? ShortName { get; }

        internal string[]? Arguments { get; }

        internal bool HelpRequested { get; }

        private static Argument<string> ShortNameArgument { get; } = new Argument<string>("template-short-name")
        {
            Arity = new ArgumentArity(0, 1)
        };

        private static Argument<string[]> RemainingArguments { get; } = new Argument<string[]>("template-args")
        {
            Arity = new ArgumentArity(0, 999)
        };

        private static Option<bool> HelpOption { get; } = new Option<bool>(new string[] { "-h", "--help", "-?" });

        internal static void AddToCommand(Command command)
        {
            command.AddArgument(ShortNameArgument);
            command.AddArgument(RemainingArguments);
            InstallCommandArgs.AddLegacyOptionsToCommand(command);
            command.AddOption(HelpOption);
        }
    }
}
