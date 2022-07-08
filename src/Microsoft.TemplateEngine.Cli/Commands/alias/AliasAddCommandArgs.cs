// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class AliasAddCommandArgs : GlobalArgs
    {
        public AliasAddCommandArgs(BaseAliasAddCommand command, ParseResult parseResult) : base(command, parseResult)
        {
        }
    }
}
