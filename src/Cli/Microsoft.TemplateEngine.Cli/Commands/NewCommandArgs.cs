// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class NewCommandArgs : GlobalArgs
    {
        public NewCommandArgs(NewCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            List<CliToken> tokensToEvaluate = new();
            foreach (var childrenResult in parseResult.CommandResult.Children)
            {
                if (childrenResult is OptionResult o)
                {
                    if (IsHelpOption(o))
                    {
                        continue;
                    }
                    if (!command.LegacyOptions.Contains(o.Option) && !command.PassByOptions.Contains(o.Option))
                    {
                        continue;
                    }

                    if (o.IdentifierToken is { } token) { tokensToEvaluate.Add(token); }
                    tokensToEvaluate.AddRange(o.Tokens);
                }
                else
                {
                    tokensToEvaluate.AddRange(childrenResult.Tokens);
                }
            }

            Tokens = tokensToEvaluate
                .Select(t => t.Value).ToArray();
        }

        internal string[] Tokens { get; }

        private static bool IsHelpOption(SymbolResult result)
            => result is OptionResult optionResult && optionResult.Option is HelpOption;
    }
}
