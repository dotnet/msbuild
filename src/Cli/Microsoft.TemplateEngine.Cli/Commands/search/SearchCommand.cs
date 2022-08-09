// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class SearchCommand : BaseSearchCommand
    {
        public SearchCommand(
                NewCommand parentCommand,
                Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "search")
        {
            parentCommand.AddNoLegacyUsageValidators(this);
        }
    }
}
