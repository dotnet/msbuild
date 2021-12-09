// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class NewCommandArgs : GlobalArgs
    {
        public NewCommandArgs(NewCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            var helpTokens = parseResult.CommandResult.Children
                .OfType<OptionResult>()
                .Where(result => IsHelpOption(result))
                .Select(result => result.Token);

            Tokens = parseResult.Tokens
                .Where(t => !helpTokens.Contains(t) && !string.IsNullOrWhiteSpace(t?.Value))
                .Select(t => t.Value).ToArray();
        }

        internal string[] Tokens { get; }

        private bool IsHelpOption(SymbolResult result)
        {
            if (result is not OptionResult optionResult)
            {
                return false;
            }
            if (optionResult.Option.HasAlias("-h"))
            {
                return true;
            }
            return false;
        }
    }
}
