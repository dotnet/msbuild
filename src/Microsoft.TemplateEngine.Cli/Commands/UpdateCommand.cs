// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UpdateCommand : BaseUpdateCommand
    {
        public UpdateCommand(
                NewCommand parentCommand,
                ITemplateEngineHost host,
                ITelemetryLogger logger,
                NewCommandCallbacks callbacks)
            : base(parentCommand, host, logger, callbacks, "update", SymbolStrings.Command_Update_Description)
        {
            parentCommand.AddNoLegacyUsageValidators(this);
            this.AddOption(CheckOnlyOption);
        }

        internal Option<bool> CheckOnlyOption { get; } = new(new[] { "--check-only", "--dry-run" })
        {
            Description = SymbolStrings.Command_Update_Option_CheckOnly
        };
    }

    internal class LegacyUpdateApplyCommand : BaseUpdateCommand
    {
        public LegacyUpdateApplyCommand(
            NewCommand parentCommand,
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks)
            : base(parentCommand, host, logger, callbacks, "--update-apply", SymbolStrings.Command_Legacy_Update_Check_Description)
        {
            this.IsHidden = true;
            parentCommand.AddNoLegacyUsageValidators(this, except: new Option[] { InteractiveOption, AddSourceOption });
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<IReadOnlyList<string>> AddSourceOption => ParentCommand.AddSourceOption;
    }

    internal class LegacyUpdateCheckCommand : BaseUpdateCommand
    {
        public LegacyUpdateCheckCommand(
            NewCommand parentCommand,
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks)
            : base(parentCommand, host, logger, callbacks, "--update-check", SymbolStrings.Command_Update_Description)
        {
            this.IsHidden = true;
            parentCommand.AddNoLegacyUsageValidators(this, except: new Option[] { InteractiveOption, AddSourceOption });
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<IReadOnlyList<string>> AddSourceOption => ParentCommand.AddSourceOption;
    }

    internal class BaseUpdateCommand : BaseCommand<UpdateCommandArgs>
    {
        internal BaseUpdateCommand(
            NewCommand parentCommand,
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks,
            string commandName,
            string description)
            : base(host, logger, callbacks, commandName, description)
        {
            ParentCommand = parentCommand;
            this.AddOption(InteractiveOption);
            this.AddOption(AddSourceOption);
        }

        internal virtual Option<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

        internal virtual Option<IReadOnlyList<string>> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption();

        protected NewCommand ParentCommand { get; }

        protected override async Task<NewCommandStatus> ExecuteAsync(UpdateCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            TemplatePackageCoordinator templatePackageCoordinator = new TemplatePackageCoordinator(
                TelemetryLogger,
                environmentSettings,
                templatePackageManager);

            //we need to await, otherwise templatePackageManager will be disposed.
            return await templatePackageCoordinator.EnterUpdateFlowAsync(args, context.GetCancellationToken()).ConfigureAwait(false);
        }

        protected override UpdateCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }

    internal class UpdateCommandArgs : GlobalArgs
    {
        public UpdateCommandArgs(BaseUpdateCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            if (command is UpdateCommand updateCommand)
            {
                CheckOnly = parseResult.GetValueForOption(updateCommand.CheckOnlyOption);
            }
            else if (command is LegacyUpdateCheckCommand)
            {
                CheckOnly = true;
            }
            else if (command is LegacyUpdateApplyCommand)
            {
                CheckOnly = false;
            }
            else
            {
                throw new ArgumentException($"Unsupported type {command.GetType().FullName}", nameof(command));
            }

            Interactive = parseResult.GetValueForOption(command.InteractiveOption);
            AdditionalSources = parseResult.GetValueForOption(command.AddSourceOption);
        }

        public bool CheckOnly { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }
    }
}
