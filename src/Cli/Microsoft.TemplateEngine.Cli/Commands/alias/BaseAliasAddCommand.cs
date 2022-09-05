// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseAliasAddCommand : BaseCommand<AliasAddCommandArgs>
    {
        internal BaseAliasAddCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName)
            : base(hostBuilder, commandName, SymbolStrings.Command_AliasAdd_Description) { }

        protected override Task<NewCommandStatus> ExecuteAsync(
            AliasAddCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            InvocationContext context) => throw new NotImplementedException();

        protected override AliasAddCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
