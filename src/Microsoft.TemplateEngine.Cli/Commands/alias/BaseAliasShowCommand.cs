// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseAliasShowCommand : BaseCommand<AliasShowCommandArgs>
    {
        internal BaseAliasShowCommand(
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks,
            string commandName)
            : base(host, logger, callbacks, commandName, SymbolStrings.Command_AliasShow_Description) { }

        protected override Task<NewCommandStatus> ExecuteAsync(AliasShowCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override AliasShowCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
