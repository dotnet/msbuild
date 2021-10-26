// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UpdateCommand : BaseUpdateCommand
    {
        public UpdateCommand(
                NewCommand newCommand,
                ITemplateEngineHost host,
                ITelemetryLogger logger,
                NewCommandCallbacks callbacks)
            : base(newCommand, host, logger, callbacks, "update")
        {
            AddValidator(symbolResult => ValidateOptionUsageInParent(symbolResult, newCommand.InteractiveOption));
            AddValidator(symbolResult => ValidateOptionUsageInParent(symbolResult, newCommand.AddSourceOption));

            this.AddOption(CheckOnlyOption);
        }

        internal Option<bool> CheckOnlyOption { get; } = new(new[] { "--check-only", "--dry-run" })
        {
            Description = LocalizableStrings.OptionDescriptionCheckOnly
        };
    }

    internal class LegacyUpdateApplyCommand : BaseUpdateCommand
    {
        public LegacyUpdateApplyCommand(NewCommand newCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
            : base(newCommand, host, logger, callbacks, "--update-apply")
        {
            this.IsHidden = true;
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<IReadOnlyList<string>> AddSourceOption => ParentCommand.AddSourceOption;
    }

    internal class LegacyUpdateCheckCommand : BaseUpdateCommand
    {
        public LegacyUpdateCheckCommand(NewCommand newCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
            : base(newCommand, host, logger, callbacks, "--update-check")
        {
            this.IsHidden = true;
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<IReadOnlyList<string>> AddSourceOption => ParentCommand.AddSourceOption;
    }

    internal class BaseUpdateCommand : BaseCommand<UpdateCommandArgs>
    {
        internal BaseUpdateCommand(NewCommand parentCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks, string commandName) : base(host, logger, callbacks, commandName)
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
            TemplateInformationCoordinator templateInformationCoordinator = new TemplateInformationCoordinator(
                environmentSettings,
                templatePackageManager,
                new TemplateCreator(environmentSettings),
                new HostSpecificDataLoader(environmentSettings),
                TelemetryLogger,
                environmentSettings.GetDefaultLanguage());

            TemplatePackageCoordinator templatePackageCoordinator = new TemplatePackageCoordinator(
                TelemetryLogger,
                environmentSettings,
                templatePackageManager,
                templateInformationCoordinator);

            //TODO: we need to await, otherwise templatePackageManager will be disposed.
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
