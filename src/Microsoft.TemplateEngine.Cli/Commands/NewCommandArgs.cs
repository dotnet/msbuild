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
            Tokens = parseResult.Tokens.Select(t => t.Value).ToArray();
            HelpRequested = parseResult.GetValueForOption<bool>(command.HelpOption);
        }

        internal string[] Tokens { get; }

        internal bool HelpRequested { get; }
    }
}
