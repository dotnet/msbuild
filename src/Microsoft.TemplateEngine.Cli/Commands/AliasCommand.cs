// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class AliasCommand : BaseCommand<AliasCommandArgs>
    {
        internal AliasCommand(
            ITemplateEngineHost host,
            ITelemetryLogger telemetryLogger,
            NewCommandCallbacks callbacks)
            : base(host, telemetryLogger, callbacks, "alias", SymbolStrings.Command_Alias_Description)
        {
            IsHidden = true;
            this.Add(new AliasAddCommand(host, telemetryLogger, callbacks));
            this.Add(new AliasShowCommand(host, telemetryLogger, callbacks));
        }

        protected override Task<NewCommandStatus> ExecuteAsync(AliasCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override AliasCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }

    internal class AliasCommandArgs : GlobalArgs
    {
        public AliasCommandArgs(AliasCommand command, ParseResult parseResult) : base(command, parseResult)
        {
        }
    }
}
