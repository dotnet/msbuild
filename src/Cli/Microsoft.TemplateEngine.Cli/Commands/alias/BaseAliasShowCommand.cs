// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseAliasShowCommand : BaseCommand<AliasShowCommandArgs>
    {
        internal BaseAliasShowCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder,
            string commandName)
            : base(hostBuilder, telemetryLoggerBuilder, commandName, SymbolStrings.Command_AliasShow_Description) { }

        protected override Task<NewCommandStatus> ExecuteAsync(
            AliasShowCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            ITelemetryLogger telemetryLogger,
            InvocationContext context) => throw new NotImplementedException();

        protected override AliasShowCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
