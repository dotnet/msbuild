// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstantiateCommandArgs : GlobalArgs
    {
        public InstantiateCommandArgs(InstantiateCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            RemainingArguments = parseResult.GetValueForArgument(command.RemainingArguments) ?? Array.Empty<string>();
            ShortName = parseResult.GetValueForArgument(command.ShortNameArgument);

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
    }
}
