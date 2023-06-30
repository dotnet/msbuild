// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyUpdateCheckCommand : BaseUpdateCommand
    {
        public LegacyUpdateCheckCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "--update-check", SymbolStrings.Command_Update_Description)
        {
            this.Hidden = true;
            parentCommand.AddNoLegacyUsageValidators(this, except: new CliOption[] { InteractiveOption, AddSourceOption });
        }

        internal override CliOption<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override CliOption<string[]> AddSourceOption => ParentCommand.AddSourceOption;

        protected override Task<NewCommandStatus> ExecuteAsync(UpdateCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, ParseResult parseResult, CancellationToken cancellationToken)
        {
            PrintDeprecationMessage<LegacyUpdateCheckCommand, UpdateCommand>(args.ParseResult, additionalOption: UpdateCommand.CheckOnlyOption);

            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken);
        }
    }
}
