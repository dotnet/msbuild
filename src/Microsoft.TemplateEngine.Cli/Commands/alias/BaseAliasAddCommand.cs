// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseAliasAddCommand : BaseCommand<AliasAddCommandArgs>
    {
        internal BaseAliasAddCommand(
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks,
            string commandName)
            : base(host, logger, callbacks, commandName, SymbolStrings.Command_AliasAdd_Description) { }

        protected override Task<NewCommandStatus> ExecuteAsync(AliasAddCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override AliasAddCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
