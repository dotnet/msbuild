// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UpdateCommand : BaseUpdateCommand
    {
        public UpdateCommand(
                NewCommand parentCommand,
                Func<ParseResult, ITemplateEngineHost> hostBuilder,
                Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder,
                NewCommandCallbacks callbacks)
            : base(parentCommand, hostBuilder, telemetryLoggerBuilder, callbacks, "update", SymbolStrings.Command_Update_Description)
        {
            parentCommand.AddNoLegacyUsageValidators(this);
            this.AddOption(CheckOnlyOption);
        }

        internal Option<bool> CheckOnlyOption { get; } = new(new[] { "--check-only", "--dry-run" })
        {
            Description = SymbolStrings.Command_Update_Option_CheckOnly
        };
    }
}
