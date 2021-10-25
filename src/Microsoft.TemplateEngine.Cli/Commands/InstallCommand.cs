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
    internal class InstallCommand : BaseInstallCommand
    {
        public InstallCommand(
                NewCommand parentCommand,
                ITemplateEngineHost host,
                ITelemetryLogger logger,
                NewCommandCallbacks callbacks)
            : base(parentCommand, host, logger, callbacks, "install")
        {
            AddValidator(symbolResult => ValidateOptionUsageInParent(symbolResult, parentCommand.InteractiveOption));
            AddValidator(symbolResult => ValidateOptionUsageInParent(symbolResult, parentCommand.AddSourceOption));
        }
    }

    internal class LegacyInstallCommand : BaseInstallCommand
    {
        public LegacyInstallCommand(NewCommand newCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
            : base(newCommand, host, logger, callbacks, "--install")
        {
            this.IsHidden = true;
            this.AddAlias("-i");
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<IReadOnlyList<string>> AddSourceOption => ParentCommand.AddSourceOption;
    }

    internal abstract class BaseInstallCommand : BaseCommand<InstallCommandArgs>
    {
        internal BaseInstallCommand(NewCommand parentCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks, string commandName)
            : base(host, logger, callbacks, commandName)
        {
            ParentCommand = parentCommand;
            this.AddArgument(NameArgument);
            this.AddOption(InteractiveOption);
            this.AddOption(AddSourceOption);
        }

        internal Argument<IReadOnlyList<string>> NameArgument { get; } = new("name")
        {
            Description = "Name of NuGet package or folder.",
            Arity = new ArgumentArity(1, 99)
        };

        internal virtual Option<bool> InteractiveOption { get; } = SharedOptionsFactory.GetInteractiveOption();

        internal virtual Option<IReadOnlyList<string>> AddSourceOption { get; } = SharedOptionsFactory.GetAddSourceOption();

        protected NewCommand ParentCommand { get; }

        protected override async Task<NewCommandStatus> ExecuteAsync(InstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
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
            return await templatePackageCoordinator.EnterInstallFlowAsync(args, context.GetCancellationToken()).ConfigureAwait(false);
        }

        protected override InstallCommandArgs ParseContext(ParseResult parseResult)
        {
            return new InstallCommandArgs(this, parseResult);
        }
    }

    internal class InstallCommandArgs : GlobalArgs
    {
        public InstallCommandArgs(BaseInstallCommand installCommand, ParseResult parseResult) : base(installCommand, parseResult)
        {
            TemplatePackages = parseResult.GetValueForArgument(installCommand.NameArgument)
                ?? throw new ArgumentException($"{nameof(parseResult)} should contain at least one argument for {nameof(installCommand.NameArgument)}", nameof(parseResult));

            //workaround for --install source1 --install source2 case
            if (installCommand is LegacyInstallCommand && installCommand.Aliases.Any(alias => TemplatePackages.Contains(alias)))
            {
                TemplatePackages = TemplatePackages.Where(package => !installCommand.Aliases.Contains(package)).ToList();
            }

            if (!TemplatePackages.Any())
            {
                throw new ArgumentException($"{nameof(parseResult)} should contain at least one argument for {nameof(installCommand.NameArgument)}", nameof(parseResult));
            }

            Interactive = parseResult.GetValueForOption(installCommand.InteractiveOption);
            AdditionalSources = parseResult.GetValueForOption(installCommand.AddSourceOption);
        }

        public IReadOnlyList<string> TemplatePackages { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }
    }
}
