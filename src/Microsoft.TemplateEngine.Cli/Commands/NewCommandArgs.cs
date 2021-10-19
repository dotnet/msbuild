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
            Arguments = parseResult.GetValueForArgument(command.RemainingArguments);
            ShortName = parseResult.GetValueForArgument(command.ShortNameArgument);
            HelpRequested = parseResult.GetValueForOption(command.HelpOption);
        }

        internal string? ShortName { get; }

        internal string[]? Arguments { get; }

        internal bool HelpRequested { get; }
    }
}
