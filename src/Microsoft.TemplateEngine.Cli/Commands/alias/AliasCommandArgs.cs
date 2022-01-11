// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class AliasCommandArgs : GlobalArgs
    {
        public AliasCommandArgs(AliasCommand command, ParseResult parseResult) : base(command, parseResult)
        {
        }
    }
}
