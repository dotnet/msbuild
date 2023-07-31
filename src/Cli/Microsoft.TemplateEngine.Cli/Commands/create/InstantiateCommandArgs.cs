// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstantiateCommandArgs : GlobalArgs
    {
        public InstantiateCommandArgs(InstantiateCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            RemainingArguments = parseResult.GetValue(command.RemainingArguments) ?? Array.Empty<string>();
            ShortName = parseResult.GetValue(InstantiateCommand.ShortNameArgument);

            var tokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(ShortName))
            {
                tokens.Add(ShortName);
            }
            tokens.AddRange(RemainingArguments);

            foreach (OptionResult optionResult in parseResult.CommandResult.Children.OfType<OptionResult>())
            {
                if (command.PassByOptions.Contains(optionResult.Option))
                {
                    if (optionResult.IdentifierToken is { } token)
                    {
                        tokens.Add(token.Value);
                    }
                    tokens.AddRange(optionResult.Tokens.Select(t => t.Value));
                }
            }
            TokensToInvoke = tokens.ToArray();
        }

        private InstantiateCommandArgs(string? shortName, IEnumerable<string> remainingArgs, GlobalArgs args) : base(args)
        {
            ShortName = shortName;
            RemainingArguments = remainingArgs.ToArray();
            var tokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(ShortName))
            {
                tokens.Add(ShortName);
            }
            tokens.AddRange(RemainingArguments);
            TokensToInvoke = tokens.ToArray();
        }

        internal string? ShortName { get; }

        internal string[] RemainingArguments { get; }

        internal string[] TokensToInvoke { get; }

        internal static InstantiateCommandArgs FromNewCommandArgs(NewCommandArgs newCommandArgs)
        {
            if (!newCommandArgs.Tokens.Any())
            {
                return new InstantiateCommandArgs(null, Array.Empty<string>(), newCommandArgs);
            }
            return new InstantiateCommandArgs(newCommandArgs.Tokens[0], newCommandArgs.Tokens.Skip(1), newCommandArgs);
        }
    }
}
