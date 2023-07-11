// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UpdateCommand : BaseUpdateCommand
    {
        public UpdateCommand(
                NewCommand parentCommand,
                Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "update", SymbolStrings.Command_Update_Description)
        {
            parentCommand.AddNoLegacyUsageValidators(this);
            this.Options.Add(CheckOnlyOption);
        }

        internal static CliOption<bool> CheckOnlyOption { get; } = new("--check-only", "--dry-run")
        {
            Description = SymbolStrings.Command_Update_Option_CheckOnly
        };

        protected override async Task<NewCommandStatus> ExecuteAsync(
            UpdateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            NewCommandStatus status = await base.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken).ConfigureAwait(false);
            await CheckTemplatesWithSubCommandName(args, templatePackageManager, cancellationToken).ConfigureAwait(false);
            return status;
        }
    }
}
